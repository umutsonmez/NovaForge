using System;
using System.Diagnostics;
using System.IO;
using NovaForge.Tiltwell;

int seed = args.Length > 0 ? int.Parse(args[0]) : 20261123;
string output = args.Length > 1 ? args[1] : "levels.json";
var sw = Stopwatch.StartNew();
var pack = LevelGenerator.Generate(seed);
File.WriteAllText(output, pack.ToJson());
Console.WriteLine($"seed={seed} levels={pack.Levels.Count} time={sw.Elapsed.TotalSeconds:F1}s -> {output}");
foreach (var l in pack.Levels)
    Console.WriteLine($"{l.Id} tier={l.Tier} par={l.Par} size={l.Rows[0].Length}x{l.Rows.Length}");
