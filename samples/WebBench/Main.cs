using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Godot;

// Simple CPU benchmark for the C# runtime on the Web.
// Each workload runs once to warm up (so the interpreter can tier up), then Runs times;
// the best time is reported. Prints "BENCH <name> <ms> ms (check=<n>)" per workload and
// "BENCH DONE total=<ms>" at the end, and shows the same text in the Label.
public partial class Main : Control
{
    private const int Runs = 3;

    private Label _output;
    private readonly StringBuilder _text = new();
    private int _frames;

    public override void _Ready()
    {
        _output = GetNode<Label>("Output");
        _output.AddThemeFontSizeOverride("font_size", 20);
        Line($"BENCH runtime {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
    }

    public override void _Process(double delta)
    {
        // Let a couple of frames render first so the page is fully up.
        if (++_frames != 3)
        {
            return;
        }

        double total = 0;
        total += Bench("int-math", IntMath);
        total += Bench("double-math", DoubleMath);
        total += Bench("list-sort", ListSort);
        total += Bench("dictionary", DictionaryOps);
        total += Bench("string-builder", StringBuilding);
        total += Bench("linq", Linq);
        total += Bench("vector2", Vector2Math);
        total += Bench("virtual-calls", VirtualCalls);
        Line($"BENCH DONE total={total:F1}");
    }

    private double Bench(string name, Func<long> workload)
    {
        long check = workload();
        double best = double.MaxValue;
        for (int i = 0; i < Runs; i++)
        {
            var sw = Stopwatch.StartNew();
            long result = workload();
            sw.Stop();
            if (result != check)
            {
                Line($"BENCH ERROR {name}: result changed ({check} vs {result})");
            }
            best = Math.Min(best, sw.Elapsed.TotalMilliseconds);
        }
        Line($"BENCH {name} {best:F1} ms (check={check})");
        return best;
    }

    private void Line(string line)
    {
        GD.Print(line);
        _text.AppendLine(line);
        _output.Text = _text.ToString();
    }

    private static long IntMath()
    {
        uint x = 2463534242;
        long sum = 0;
        for (int i = 0; i < 2_000_000; i++)
        {
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            sum += (x % 1000) * 3 + (i & 7);
        }
        return sum;
    }

    private static long DoubleMath()
    {
        double acc = 0;
        for (int i = 1; i < 1_000_000; i++)
        {
            double d = i;
            acc += Math.Sqrt(d) * 0.5 + 1.0 / d;
        }
        return (long)acc;
    }

    private static long ListSort()
    {
        var rng = new Random(1234);
        var list = new List<int>(200_000);
        for (int i = 0; i < 200_000; i++)
        {
            list.Add(rng.Next());
        }
        list.Sort();
        long sum = 0;
        for (int i = 0; i < list.Count; i += 1000)
        {
            sum += list[i] % 1000;
        }
        return sum;
    }

    private static long DictionaryOps()
    {
        var dict = new Dictionary<int, int>();
        for (int i = 0; i < 100_000; i++)
        {
            dict[i * 7] = i;
        }
        long sum = 0;
        for (int i = 0; i < 200_000; i++)
        {
            if (dict.TryGetValue(i, out int v))
            {
                sum += v;
            }
        }
        return sum + dict.Count;
    }

    private static long StringBuilding()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 100_000; i++)
        {
            sb.Append("item").Append(i).Append(',');
        }
        string s = sb.ToString();
        return s.Length + s.Split(',').Length;
    }

    private static long Linq()
    {
        return Enumerable.Range(0, 500_000)
            .Where(i => i % 3 == 0)
            .Select(i => (long)i * 2)
            .Sum();
    }

    private static long Vector2Math()
    {
        var v = Vector2.Zero;
        var step = new Vector2(0.5f, 0.25f);
        for (int i = 0; i < 500_000; i++)
        {
            v = (v + step).Rotated(0.001f) * 0.999f;
        }
        return (long)(v.Length() * 1000);
    }

    private abstract class Shape
    {
        public abstract int Area();
    }

    private sealed class Square : Shape
    {
        public int Side;
        public override int Area() => Side * Side;
    }

    private sealed class Rect : Shape
    {
        public int W, H;
        public override int Area() => W * H;
    }

    private static long VirtualCalls()
    {
        var shapes = new Shape[64];
        for (int i = 0; i < shapes.Length; i++)
        {
            shapes[i] = (i & 1) == 0 ? new Square { Side = i } : new Rect { W = i, H = 3 };
        }
        long sum = 0;
        for (int n = 0; n < 20_000; n++)
        {
            for (int i = 0; i < shapes.Length; i++)
            {
                sum += shapes[i].Area();
            }
        }
        return sum;
    }
}
