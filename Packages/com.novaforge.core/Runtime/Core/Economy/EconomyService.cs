using System;
using System.Collections.Generic;
using NovaForge.Core.Data;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Economy
{
    public enum TransactionStatus
    {
        Success,
        /// <summary>Same transaction id was already applied. Nothing changed. Callers should treat as "already done".</summary>
        Duplicate,
        InsufficientFunds,
        UnknownResource,
        InvalidAmount,
        DailyLimitReached,
        ExceedsMaximum,
        InvalidTransactionId,
        PersistenceFailed,
    }

    /// <summary>A set of resource changes applied all-or-nothing.</summary>
    public sealed class EconomyTransaction
    {
        private readonly List<ResourceAmount> _deltas = new List<ResourceAmount>();

        /// <summary>Unique id. Re-applying the same id is rejected as <see cref="TransactionStatus.Duplicate"/>.</summary>
        public string Id { get; }
        /// <summary>Why it happened: "level_reward", "shop", "iap", "rewarded_ad", "daily_reward"...</summary>
        public string Source { get; }
        public IReadOnlyList<ResourceAmount> Deltas => _deltas;

        public EconomyTransaction(string id, string source)
        {
            Id = id;
            Source = Guard.Id(source, nameof(source));
        }

        public EconomyTransaction Credit(string resourceId, long amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Credit amount must be positive.");
            _deltas.Add(new ResourceAmount(resourceId, amount));
            return this;
        }

        public EconomyTransaction Debit(string resourceId, long amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Debit amount must be positive.");
            _deltas.Add(new ResourceAmount(resourceId, -amount));
            return this;
        }
    }

    public sealed class TransactionRecord
    {
        public string Id;
        public string Source;
        public long UnixMs;
        public List<string> ResourceIds = new List<string>();
        public List<long> Deltas = new List<long>();
    }

    public readonly struct BalanceChanged
    {
        public readonly string ResourceId;
        public readonly long OldAmount;
        public readonly long NewAmount;
        public readonly string Source;
        public BalanceChanged(string resourceId, long oldAmount, long newAmount, string source)
        { ResourceId = resourceId; OldAmount = oldAmount; NewAmount = newAmount; Source = source; }
    }

    /// <summary>Persisted economy state.</summary>
    public sealed class EconomyState : ISaveValidatable
    {
        public Dictionary<string, long> Balances = new Dictionary<string, long>();
        /// <summary>Recently applied transaction ids (bounded FIFO) for duplicate protection.</summary>
        public List<string> AppliedIds = new List<string>();
        /// <summary>"dayIndex|resource|source" -> earned amount</summary>
        public Dictionary<string, long> DailyEarned = new Dictionary<string, long>();
        public List<TransactionRecord> History = new List<TransactionRecord>();

        public string Validate()
        {
            if (Balances == null || AppliedIds == null || DailyEarned == null || History == null) return "null collection";
            foreach (var kv in Balances) if (kv.Value < 0) return $"negative balance {kv.Key}={kv.Value}";
            return null;
        }
    }

    public interface IWallet
    {
        long GetBalance(string resourceId);
        bool CanAfford(IReadOnlyList<ResourceAmount> cost);
        TransactionStatus Apply(EconomyTransaction transaction);
        bool WasApplied(string transactionId);
        /// <summary>How much more of resource can still be earned today from the source; long.MaxValue when unlimited.</summary>
        long RemainingDailyEarn(string resourceId, string source);
        IReadOnlyList<TransactionRecord> History { get; }
    }

    /// <summary>
    /// Authoritative local ledger for currencies and items.
    /// Invariants enforced on every transaction:
    ///  - balances never negative, never above the declared maximum;
    ///  - all deltas apply together or none do;
    ///  - a transaction id applies at most once (double reward / replay protection);
    ///  - daily earn caps per (resource, source);
    ///  - state is persisted before success is reported; on persistence failure the change is rolled back.
    /// </summary>
    public sealed class EconomyService : IWallet
    {
        public const int SaveVersion = 1;
        private const string Tag = "Economy";
        private readonly EconomyDefinition _definition;
        private readonly SaveSlot<EconomyState> _slot;
        private readonly IClock _clock;
        private readonly ILog _log;
        private readonly IEventBus _events;
        private readonly int _maxAppliedIds;
        private readonly int _maxHistory;
        private readonly HashSet<string> _appliedLookup = new HashSet<string>();
        private EconomyState _state;

        public EconomyService(EconomyDefinition definition, SaveManager saves, IClock clock, ILog log, IEventBus events,
            int maxRememberedTransactionIds = 5000, int maxHistory = 200)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _events = events;
            _maxAppliedIds = maxRememberedTransactionIds;
            _maxHistory = maxHistory;
            _slot = new SaveSlot<EconomyState>(saves, "economy", SaveVersion, CreateDefault);
            Load();
        }

        public IReadOnlyList<TransactionRecord> History => _state.History;

        public long GetBalance(string resourceId)
        {
            return _state.Balances.TryGetValue(resourceId ?? string.Empty, out var v) ? v : 0;
        }

        public bool CanAfford(IReadOnlyList<ResourceAmount> cost)
        {
            if (cost == null) return true;
            var totals = new Dictionary<string, long>();
            foreach (var c in cost)
            {
                if (c.Amount < 0) return false;
                totals.TryGetValue(c.ResourceId, out var t);
                totals[c.ResourceId] = t + c.Amount;
            }
            foreach (var kv in totals) if (GetBalance(kv.Key) < kv.Value) return false;
            return true;
        }

        public bool WasApplied(string transactionId) => transactionId != null && _appliedLookup.Contains(transactionId);

        public long RemainingDailyEarn(string resourceId, string source)
        {
            long remaining = long.MaxValue;
            int day = Today();
            foreach (var limit in _definition.Limits)
            {
                if (limit.ResourceId != resourceId || limit.Source != source) continue;
                _state.DailyEarned.TryGetValue(DailyKey(day, resourceId, source), out var earned);
                remaining = Math.Min(remaining, Math.Max(0, limit.MaxPerDay - earned));
            }
            return remaining;
        }

        public TransactionStatus Apply(EconomyTransaction tx)
        {
            if (tx == null) throw new ArgumentNullException(nameof(tx));
            if (string.IsNullOrEmpty(tx.Id) || tx.Id.Length > 128) return Reject(tx, TransactionStatus.InvalidTransactionId);
            if (_appliedLookup.Contains(tx.Id)) return Reject(tx, TransactionStatus.Duplicate);
            if (tx.Deltas.Count == 0) return Reject(tx, TransactionStatus.InvalidAmount);

            // 1) Aggregate deltas per resource and validate against definitions.
            var net = new Dictionary<string, long>();
            var earned = new Dictionary<string, long>();
            foreach (var d in tx.Deltas)
            {
                if (!_definition.TryGet(d.ResourceId, out _)) return Reject(tx, TransactionStatus.UnknownResource);
                net.TryGetValue(d.ResourceId, out var n);
                try { net[d.ResourceId] = checked(n + d.Amount); }
                catch (OverflowException) { return Reject(tx, TransactionStatus.InvalidAmount); }
                if (d.Amount > 0)
                {
                    earned.TryGetValue(d.ResourceId, out var e);
                    earned[d.ResourceId] = e + d.Amount;
                }
            }

            // 2) Check resulting balances.
            var newBalances = new Dictionary<string, long>();
            foreach (var kv in net)
            {
                _definition.TryGet(kv.Key, out var def);
                long current = GetBalance(kv.Key);
                long next;
                try { next = checked(current + kv.Value); }
                catch (OverflowException) { return Reject(tx, TransactionStatus.ExceedsMaximum); }
                if (next < 0) return Reject(tx, TransactionStatus.InsufficientFunds);
                if (next > def.MaxAmount) return Reject(tx, TransactionStatus.ExceedsMaximum);
                newBalances[kv.Key] = next;
            }

            // 3) Daily earn caps.
            foreach (var kv in earned)
                if (kv.Value > RemainingDailyEarn(kv.Key, tx.Source)) return Reject(tx, TransactionStatus.DailyLimitReached);

            // 4) Commit in memory, persist, roll back on failure.
            var snapshot = CloneState(_state);
            int day = Today();
            var changes = new List<BalanceChanged>();
            foreach (var kv in newBalances)
            {
                long old = GetBalance(kv.Key);
                _state.Balances[kv.Key] = kv.Value;
                if (old != kv.Value) changes.Add(new BalanceChanged(kv.Key, old, kv.Value, tx.Source));
            }
            foreach (var kv in earned)
            {
                var key = DailyKey(day, kv.Key, tx.Source);
                _state.DailyEarned.TryGetValue(key, out var e);
                _state.DailyEarned[key] = e + kv.Value;
            }
            PruneDaily(day);
            RememberId(tx.Id);
            AddHistory(tx);

            try
            {
                _slot.Save(_state);
            }
            catch (Exception ex)
            {
                _state = snapshot;
                RebuildLookup();
                _log.Error(Tag, $"Transaction '{tx.Id}' rolled back: save failed.", ex);
                return TransactionStatus.PersistenceFailed;
            }

            foreach (var c in changes) _events?.Publish(c);
            return TransactionStatus.Success;
        }

        private TransactionStatus Reject(EconomyTransaction tx, TransactionStatus status)
        {
            if (status != TransactionStatus.Duplicate && status != TransactionStatus.InsufficientFunds)
                _log.Warning(Tag, $"Transaction '{tx.Id}' ({tx.Source}) rejected: {status}");
            else
                _log.Debug(Tag, $"Transaction '{tx.Id}' ({tx.Source}) rejected: {status}");
            return status;
        }

        private void Load()
        {
            var result = _slot.Load();
            _state = result.Data;
            // Ensure every declared resource exists (new resources added in a game update start at their initial amount).
            foreach (var def in _definition.Resources)
                if (!_state.Balances.ContainsKey(def.Id)) _state.Balances[def.Id] = def.InitialAmount;
            RebuildLookup();
        }

        private EconomyState CreateDefault()
        {
            var s = new EconomyState();
            foreach (var def in _definition.Resources) s.Balances[def.Id] = def.InitialAmount;
            return s;
        }

        private void RememberId(string id)
        {
            _state.AppliedIds.Add(id);
            _appliedLookup.Add(id);
            while (_state.AppliedIds.Count > _maxAppliedIds)
            {
                _appliedLookup.Remove(_state.AppliedIds[0]);
                _state.AppliedIds.RemoveAt(0);
            }
        }

        private void AddHistory(EconomyTransaction tx)
        {
            var rec = new TransactionRecord { Id = tx.Id, Source = tx.Source, UnixMs = TimeUtil.ToUnixMs(_clock.UtcNow) };
            foreach (var d in tx.Deltas) { rec.ResourceIds.Add(d.ResourceId); rec.Deltas.Add(d.Amount); }
            _state.History.Add(rec);
            while (_state.History.Count > _maxHistory) _state.History.RemoveAt(0);
        }

        private void PruneDaily(int today)
        {
            List<string> stale = null;
            foreach (var key in _state.DailyEarned.Keys)
            {
                var sep = key.IndexOf('|');
                if (sep <= 0 || !int.TryParse(key.Substring(0, sep), out var day) || day < today - 1)
                    (stale ??= new List<string>()).Add(key);
            }
            if (stale != null) foreach (var k in stale) _state.DailyEarned.Remove(k);
        }

        private void RebuildLookup()
        {
            _appliedLookup.Clear();
            foreach (var id in _state.AppliedIds) _appliedLookup.Add(id);
        }

        private int Today() => TimeUtil.DayIndex(_clock.UtcNow, _definition.DayResetOffset);

        private static string DailyKey(int day, string resource, string source) => day + "|" + resource + "|" + source;

        private static EconomyState CloneState(EconomyState s)
        {
            var c = new EconomyState
            {
                Balances = new Dictionary<string, long>(s.Balances),
                AppliedIds = new List<string>(s.AppliedIds),
                DailyEarned = new Dictionary<string, long>(s.DailyEarned),
                History = new List<TransactionRecord>(s.History),
            };
            return c;
        }
    }
}
