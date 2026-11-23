using System;
using System.Collections.Generic;
using NovaForge.Core.Foundation;

namespace NovaForge.Core.Economy
{
    public enum ResourceKind
    {
        /// <summary>Soft/hard currency: coin, gem, energy, ticket, star...</summary>
        Currency,
        /// <summary>Inventory item: hint, booster, cosmetic...</summary>
        Item,
    }

    /// <summary>
    /// A countable thing the player owns. Currencies and items share one model so a single atomic
    /// transaction can e.g. debit coins and credit a booster.
    /// Each game declares only the resources it needs.
    /// </summary>
    public sealed class ResourceDefinition
    {
        public string Id { get; }
        public ResourceKind Kind { get; }
        /// <summary>Localization key for the display name.</summary>
        public string NameKey { get; }
        public long InitialAmount { get; }
        public long MaxAmount { get; }

        public ResourceDefinition(string id, ResourceKind kind, string nameKey, long initialAmount = 0, long maxAmount = long.MaxValue)
        {
            Id = Guard.Id(id, nameof(id));
            Kind = kind;
            NameKey = nameKey ?? id;
            if (initialAmount < 0) throw new ArgumentOutOfRangeException(nameof(initialAmount));
            if (maxAmount < initialAmount) throw new ArgumentOutOfRangeException(nameof(maxAmount));
            InitialAmount = initialAmount;
            MaxAmount = maxAmount;
        }
    }

    /// <summary>Caps how much of a resource can be EARNED from one source per day (e.g. ad rewards).</summary>
    public sealed class DailyEarnLimit
    {
        public string ResourceId { get; }
        public string Source { get; }
        public long MaxPerDay { get; }

        public DailyEarnLimit(string resourceId, string source, long maxPerDay)
        {
            ResourceId = Guard.Id(resourceId, nameof(resourceId));
            Source = Guard.Id(source, nameof(source));
            if (maxPerDay < 0) throw new ArgumentOutOfRangeException(nameof(maxPerDay));
            MaxPerDay = maxPerDay;
        }
    }

    public readonly struct ResourceAmount
    {
        public readonly string ResourceId;
        public readonly long Amount;
        public ResourceAmount(string resourceId, long amount) { ResourceId = resourceId; Amount = amount; }
        public override string ToString() => $"{Amount} {ResourceId}";
    }

    public sealed class EconomyDefinition
    {
        private readonly Dictionary<string, ResourceDefinition> _resources = new Dictionary<string, ResourceDefinition>();
        private readonly List<DailyEarnLimit> _limits = new List<DailyEarnLimit>();

        /// <summary>When the "day" rolls over for daily limits, relative to UTC midnight.</summary>
        public TimeSpan DayResetOffset { get; set; } = TimeSpan.Zero;

        public IEnumerable<ResourceDefinition> Resources => _resources.Values;
        public IReadOnlyList<DailyEarnLimit> Limits => _limits;

        public EconomyDefinition AddResource(ResourceDefinition def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (_resources.ContainsKey(def.Id)) throw new ArgumentException($"Resource '{def.Id}' declared twice.");
            _resources[def.Id] = def;
            return this;
        }

        public EconomyDefinition AddCurrency(string id, long initial = 0, long max = long.MaxValue, string nameKey = null)
            => AddResource(new ResourceDefinition(id, ResourceKind.Currency, nameKey ?? "currency." + id, initial, max));

        public EconomyDefinition AddItem(string id, long initial = 0, long max = long.MaxValue, string nameKey = null)
            => AddResource(new ResourceDefinition(id, ResourceKind.Item, nameKey ?? "item." + id, initial, max));

        public EconomyDefinition AddDailyEarnLimit(string resourceId, string source, long maxPerDay)
        {
            if (!_resources.ContainsKey(resourceId)) throw new ArgumentException($"Unknown resource '{resourceId}'.");
            _limits.Add(new DailyEarnLimit(resourceId, source, maxPerDay));
            return this;
        }

        public bool TryGet(string id, out ResourceDefinition def) => _resources.TryGetValue(id ?? string.Empty, out def);
    }
}
