using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Godot;

// Checks the parts of the .NET runtime that depend on the JS side of the browser-wasm runtime
// (timers, background jobs, entropy, time zones, ...). Prints one "WRT PASS|FAIL|INFO <name>: <detail>"
// line per check and a final "WRT DONE pass=N fail=M" line, and shows the same text in a Label.
public partial class Main : Control
{
    private const double TimeoutSeconds = 5.0;

    private Label _output;
    private readonly StringBuilder _text = new();
    private readonly Dictionary<string, string> _pending = new();
    private int _pass;
    private int _fail;
    private double _elapsed;
    private bool _done;

    private Stopwatch _stopwatch;
    private System.Threading.Timer _timer;
    private volatile bool _timerFired;

    public override void _Ready()
    {
        _output = GetNode<Label>("Output");
        Report("INFO", "runtime", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);

        _stopwatch = Stopwatch.StartNew();

        Pending("Task.Delay(100)");
        Pending("System.Threading.Timer");
        Pending("Task.Yield continuation");
        Pending("Task.Run");
        Pending("Stopwatch");

        RunSyncChecks();
        RunStep7bChecks();

        _ = DelayCheck();
        _ = YieldCheck();
        _ = TaskRunCheck();

        _timer = new System.Threading.Timer(_ => _timerFired = true, null, 50, Timeout.Infinite);
    }

    public override void _Process(double delta)
    {
        if (_done)
            return;
        _elapsed += delta;

        if (_timerFired)
            Complete("System.Threading.Timer", true, "callback ran");

        if (_pending.ContainsKey("Stopwatch") && _elapsed > 0.5)
        {
            long ms = _stopwatch.ElapsedMilliseconds;
            Complete("Stopwatch", ms >= 250, $"{ms} ms elapsed after {_elapsed:F2} s of frames");
        }

        if (_pending.Count == 0 || _elapsed > TimeoutSeconds)
        {
            foreach (var name in _pending.Keys.ToList())
                Complete(name, false, $"did not complete within {TimeoutSeconds} s");
            _done = true;
            Emit($"WRT DONE pass={_pass} fail={_fail}");
        }
    }

    private async Task DelayCheck()
    {
        var sw = Stopwatch.StartNew();
        await Task.Delay(100);
        Complete("Task.Delay(100)", true, $"completed after {sw.ElapsedMilliseconds} ms");
    }

    private async Task YieldCheck()
    {
        int before = System.Environment.CurrentManagedThreadId;
        await Task.Yield();
        Complete("Task.Yield continuation", true, $"thread {before} -> {System.Environment.CurrentManagedThreadId}");
    }

    private async Task TaskRunCheck()
    {
        int value = await Task.Run(() => 21 * 2);
        Complete("Task.Run", value == 42, $"result {value}");
    }

    private void RunSyncChecks()
    {
        Check("Random.Shared", () =>
        {
            int a = Random.Shared.Next();
            int b = Random.Shared.Next();
            int c = new Random().Next();
            return (a != b, $"values {a} {b} new Random(): {c} (compare across runs)");
        });

        Check("Guid.NewGuid", () =>
        {
            Guid a = Guid.NewGuid();
            Guid b = Guid.NewGuid();
            return (a != b && a != Guid.Empty, $"{a} {b} (compare across runs)");
        });

        Check("RandomNumberGenerator.GetBytes", () =>
        {
            byte[] a = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
            byte[] b = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
            bool ok = !a.SequenceEqual(b) && a.Any(x => x != 0) && b.Any(x => x != 0);
            return (ok, $"{Convert.ToHexString(a)} {Convert.ToHexString(b)}");
        });

        Check("DateTime.UtcNow", () =>
        {
            double jsNow = JavaScriptBridge.Eval("Date.now()").AsDouble();
            double csNow = (DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;
            double diff = Math.Abs(jsNow - csNow);
            return (diff < 2000, $"{DateTime.UtcNow:O}, differs from JS Date.now() by {diff:F0} ms");
        });

        Check("DateTime.Now offset", () =>
        {
            int jsOffsetMin = -(int)JavaScriptBridge.Eval("new Date().getTimezoneOffset()").AsDouble();
            double csOffsetMin = (DateTime.Now - DateTime.UtcNow).TotalMinutes;
            bool ok = Math.Abs(csOffsetMin - jsOffsetMin) < 1;
            return (ok, $"Now={DateTime.Now:O}, C# offset {csOffsetMin:F0} min, JS offset {jsOffsetMin} min");
        });

        Check("TimeZoneInfo.Local", () =>
        {
            string jsZone = JavaScriptBridge.Eval("Intl.DateTimeFormat().resolvedOptions().timeZone").AsString();
            TimeZoneInfo local = TimeZoneInfo.Local;
            return (local.Id == jsZone, $"Id={local.Id} BaseUtcOffset={local.BaseUtcOffset} (JS: {jsZone})");
        });

        Check("TimeZoneInfo.FindSystemTimeZoneById", () =>
        {
            TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            return (tz.BaseUtcOffset == TimeSpan.FromHours(-5), $"America/New_York BaseUtcOffset={tz.BaseUtcOffset}");
        });

        Check("CultureInfo.CurrentCulture", () =>
        {
            string js = JavaScriptBridge.Eval("navigator.language").AsString();
            CultureInfo c = CultureInfo.CurrentCulture;
            // Informational: with InvariantGlobalization (forced on browser-wasm) the culture is invariant.
            return (true, $"Name=\"{c.Name}\" DisplayName=\"{c.DisplayName}\" (JS navigator.language: {js})");
        });

        Check("exception throw/catch", () =>
        {
            try
            {
                throw new InvalidOperationException("boom");
            }
            catch (InvalidOperationException e)
            {
                return (e.Message == "boom" && e.StackTrace != null, "caught with stack trace");
            }
        });

        Check("string.Format numbers", () =>
        {
            string s = string.Format(CultureInfo.InvariantCulture, "{0:N2}|{1}|{2:X}|{3:E2}", 1234.5, 1.5, 255, 12345.678);
            string cur = string.Format("{0:N1}", 9876.5);
            return (s == "1,234.50|1.5|FF|1.23E+004", $"\"{s}\" current culture: \"{cur}\"");
        });

        // JS interop is not supported; this only checks that using it fails cleanly.
        Check("JSHost.GlobalThis (unsupported)", () =>
        {
            try
            {
                if (!OperatingSystem.IsBrowser())
                    return (true, "not running in a browser");
                var global = System.Runtime.InteropServices.JavaScript.JSHost.GlobalThis;
                return (true, $"returned {global}");
            }
            catch (Exception e)
            {
                return (true, $"threw {e.GetType().Name}: {e.Message}");
            }
        }, info: true);

        Check("Environment.ProcessId", () =>
        {
            int pid = System.Environment.ProcessId;
            return (true, $"{pid}");
        }, info: true);
    }

    private void Check(string name, Func<(bool ok, string detail)> check, bool info = false)
    {
        try
        {
            var (ok, detail) = check();
            Report(info ? "INFO" : ok ? "PASS" : "FAIL", name, detail);
        }
        catch (Exception e)
        {
            Report(info ? "INFO" : "FAIL", name, $"threw {e.GetType().Name}: {e.Message}");
        }
    }

    private void Pending(string name) => _pending[name] = name;

    private void Complete(string name, bool ok, string detail)
    {
        if (!_pending.Remove(name))
            return;
        Report(ok ? "PASS" : "FAIL", name, detail);
    }

    private void Report(string result, string name, string detail)
    {
        if (result == "PASS")
            _pass++;
        else if (result == "FAIL")
            _fail++;
        Emit($"WRT {result} {name}: {detail}");
    }

    private void Emit(string line)
    {
        GD.Print(line);
        _text.AppendLine(line);
        if (_output != null)
            _output.Text = _text.ToString();
    }
}
