using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Text;
using Godot;
using NVector3 = System.Numerics.Vector3;
using NVector4 = System.Numerics.Vector4;

// Step 7b checks: SIMD, globalization, exceptions (incl. across Godot callbacks) and time zones.
public partial class Main
{
    [Signal]
    public delegate void WrtSignalEventHandler(int value);

    private int _signalHandlersRun;
    private bool _deferredThrowerRan;

    private void RunStep7bChecks()
    {
        RunSimdChecks();
        RunGlobalizationChecks();
        RunNativeSignatureChecks();
        RunExceptionChecks();
        RunTimeZoneChecks();
    }

    // ---------------------------------------------------------------- SIMD

    private void RunSimdChecks()
    {
        Check("SIMD support flags", () =>
        {
            string s = $"Vector128.IsHardwareAccelerated={Vector128.IsHardwareAccelerated} " +
                $"Vector.IsHardwareAccelerated={Vector.IsHardwareAccelerated} Vector<int>.Count={Vector<int>.Count} " +
                $"PackedSimd.IsSupported={System.Runtime.Intrinsics.Wasm.PackedSimd.IsSupported} " +
                $"Vector64.IsHardwareAccelerated={Vector64.IsHardwareAccelerated} Vector256.IsHardwareAccelerated={Vector256.IsHardwareAccelerated}";
            return (true, s);
        }, info: true);

        Check("SIMD Vector128<float> arithmetic", () =>
        {
            var a = Vector128.Create(1.5f, -2f, 3.25f, 1e10f);
            var b = Vector128.Create(2f, 4f, -1f, 3f);
            var r = (a * b + a) / b - Vector128.Create(1f);
            float[] expect = new float[4];
            float[] fa = { 1.5f, -2f, 3.25f, 1e10f }, fb = { 2f, 4f, -1f, 3f };
            for (int i = 0; i < 4; i++)
                expect[i] = (fa[i] * fb[i] + fa[i]) / fb[i] - 1f;
            bool ok = Enumerable.Range(0, 4).All(i => r.GetElement(i) == expect[i]);
            float sum = Vector128.Sum(a);
            float dot = Vector128.Dot(a, b);
            ok &= sum == fa.Sum() && dot == fa.Zip(fb, (x, y) => x * y).Aggregate(0f, (x, y) => x + y);
            var sq = Vector128.Sqrt(Vector128.Create(4f, 9f, 16f, 2f));
            ok &= sq.GetElement(0) == 2f && sq.GetElement(3) == MathF.Sqrt(2f);
            return (ok, $"r={r} sum={sum} dot={dot} sqrt={sq}");
        });

        Check("SIMD Vector128<int/byte> ops", () =>
        {
            var a = Vector128.Create(1, -2, int.MaxValue, 7);
            var b = Vector128.Create(10, 20, 1, -7);
            var add = a + b; // wraps
            var min = Vector128.Min(a, b);
            var eq = Vector128.Equals(a + Vector128.Create(0, 0, 0, -14), Vector128.Create(1, -2, int.MaxValue, -7));
            var shl = a << 3;
            var x = Vector128.Create((byte)0xF0) ^ Vector128.Create((byte)0x0F);
            var shuf = Vector128.Shuffle(Vector128.Create(10, 20, 30, 40), Vector128.Create(3, 2, 1, 0));
            uint mask = Vector128.ExtractMostSignificantBits(Vector128.Create(-1, 1, -5, 0));
            bool ok = add.Equals(Vector128.Create(11, 18, int.MinValue, 0))
                && min.Equals(Vector128.Create(1, -2, 1, -7))
                && eq.Equals(Vector128.Create(-1, -1, -1, -1))
                && shl.Equals(Vector128.Create(8, -16, int.MaxValue << 3, 56))
                && x.Equals(Vector128.Create((byte)0xFF))
                && shuf.Equals(Vector128.Create(40, 30, 20, 10))
                && mask == 0b0101;
            return (ok, $"add={add} min={min} shl={shl} shuf={shuf} mask={mask}");
        });

        Check("SIMD Vector<T> loop sum", () =>
        {
            int[] data = Enumerable.Range(0, 1003).Select(i => i * 3 - 500).ToArray();
            var acc = Vector<int>.Zero;
            int i = 0;
            for (; i <= data.Length - Vector<int>.Count; i += Vector<int>.Count)
                acc += new Vector<int>(data, i);
            int sum = Vector.Sum(acc);
            for (; i < data.Length; i++)
                sum += data[i];
            int expect = data.Sum();
            return (sum == expect, $"sum={sum} expected={expect} Count={Vector<int>.Count}");
        });

        Check("SIMD PackedSimd intrinsics", () =>
        {
            if (!System.Runtime.Intrinsics.Wasm.PackedSimd.IsSupported)
                return (true, "PackedSimd.IsSupported=false (skipped)");
            var a = Vector128.Create(1, 2, 3, 4);
            var r = System.Runtime.Intrinsics.Wasm.PackedSimd.Add(a, a);
            var m = System.Runtime.Intrinsics.Wasm.PackedSimd.Multiply(Vector128.Create(1.5f), Vector128.Create(2f));
            bool ok = r.Equals(Vector128.Create(2, 4, 6, 8)) && m.Equals(Vector128.Create(3f));
            return (ok, $"add={r} mul={m}");
        });

        Check("SIMD vectorized BCL (span/string/Numerics)", () =>
        {
            var big = new byte[4099];
            big[4000] = 42;
            int idx = big.AsSpan().IndexOf((byte)42);
            var copy = (byte[])big.Clone();
            bool seq = big.AsSpan().SequenceEqual(copy);
            copy[4098] = 1;
            bool seq2 = big.AsSpan().SequenceEqual(copy);
            string s = new string('a', 1000) + "needle" + new string('b', 100);
            int sidx = s.IndexOf("needle", StringComparison.Ordinal);
            int cnt = s.AsSpan().Count('b');
            var m = Matrix4x4.CreateRotationZ(MathF.PI / 2) * Matrix4x4.CreateTranslation(1, 2, 3);
            var v = NVector3.Transform(new NVector3(1, 0, 0), m);
            var v4 = NVector4.Normalize(new NVector4(3, 4, 0, 0));
            bool ok = idx == 4000 && seq && !seq2 && sidx == 1000 && cnt == 100
                && MathF.Abs(v.X - 1f) < 1e-5f && MathF.Abs(v.Y - 3f) < 1e-5f && v.Z == 3f
                && MathF.Abs(v4.X - 0.6f) < 1e-6f;
            string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
            ok &= Encoding.UTF8.GetString(Convert.FromBase64String(b64)) == s;
            return (ok, $"IndexOf={idx} seq={seq}/{seq2} strIdx={sidx} count={cnt} v={v} v4={v4}");
        });

        // Godot's own math types (Vector3/Basis), to make sure SIMD flags on the engine side don't break them.
        Check("Godot math types", () =>
        {
            var basis = new Basis(Godot.Vector3.Up, Mathf.Pi / 2);
            var v = basis * new Godot.Vector3(1, 0, 0);
            bool ok = v.IsEqualApprox(new Godot.Vector3(0, 0, -1));
            return (ok, $"{v}");
        });
    }

    // ---------------------------------------------------------------- Globalization

    private void RunGlobalizationChecks()
    {
        bool invariant;
        string mode;
        try
        {
            var probe = new CultureInfo("de-DE");
            invariant = probe.NumberFormat.NumberDecimalSeparator == "." && probe.Name == "de-DE" && probe.EnglishName.Contains("Invariant");
            mode = $"ICU (de-DE EnglishName=\"{probe.EnglishName}\")";
        }
        catch (Exception e)
        {
            invariant = true;
            mode = $"invariant: new CultureInfo(\"de-DE\") threw {e.GetType().Name}: {e.Message}";
        }
        Report("INFO", "globalization mode", mode);

        // In invariant mode these checks can't pass; report what users get instead of failing.
        void GCheck(string name, Func<(bool ok, string detail)> check) => Check(name, check, info: invariant);

        GCheck("culture de-DE formatting", () =>
        {
            var de = new CultureInfo("de-DE");
            string n = 1234567.891.ToString("N2", de);
            string d = new DateTime(2024, 3, 9, 14, 5, 0).ToString("D", de);
            string c = 12.5m.ToString("C", de);
            bool ok = n == "1.234.567,89" && d == "Samstag, 9. März 2024" && c.StartsWith("12,50") && c.Contains('€');
            double parsed = double.Parse("1.234,5", de);
            ok &= parsed == 1234.5;
            return (ok, $"N2=\"{n}\" D=\"{d}\" C=\"{c}\" parse=\"{parsed}\"");
        });

        GCheck("culture string compare", () =>
        {
            var de = new CultureInfo("de-DE");
            var sv = new CultureInfo("sv-SE");
            int a = string.Compare("äpfel", "Zebra", false, de); // de: ä sorts with a -> < 0
            int b = string.Compare("ä", "z", false, sv);         // sv: ä after z -> > 0
            int c = string.Compare("strasse", "straße", CultureInfo.InvariantCulture, CompareOptions.IgnoreNonSpace);
            bool ign = string.Equals("RESUME", "résumé", StringComparison.InvariantCultureIgnoreCase);
            bool ok = a < 0 && b > 0 && de.CompareInfo.IndexOf("Straße", "SS", CompareOptions.IgnoreCase) == -1;
            return (ok, $"de(äpfel,Zebra)={a} sv(ä,z)={b} inv(strasse,straße)={c} IgnoreCase(RESUME,résumé)={ign}");
        });

        GCheck("culture tr-TR casing", () =>
        {
            var tr = new CultureInfo("tr-TR");
            string up = "istanbul".ToUpper(tr);
            string low = "I".ToLower(tr);
            string inv = "istanbul".ToUpperInvariant();
            bool ok = up == "İSTANBUL" && low == "ı" && inv == "ISTANBUL";
            return (ok, $"ToUpper(tr)=\"{up}\" ToLower(tr,I)=\"{low}\" ToUpperInvariant=\"{inv}\"");
        });

        GCheck("culture ja-JP / ar-SA data", () =>
        {
            var ja = new CultureInfo("ja-JP");
            string month = ja.DateTimeFormat.GetMonthName(1);
            var ar = new CultureInfo("ar-SA");
            string cal = ar.Calendar.GetType().Name;
            bool ok = month == "1月" && ja.TextInfo.IsRightToLeft == false && ar.TextInfo.IsRightToLeft;
            return (ok, $"ja month1=\"{month}\" ar calendar={cal} ar RTL={ar.TextInfo.IsRightToLeft}");
        });

        Check("CultureInfo.CurrentCulture vs browser", () =>
        {
            string js = JavaScriptBridge.Eval("navigator.language").AsString();
            CultureInfo c = CultureInfo.CurrentCulture;
            return (true, $"Name=\"{c.Name}\" (JS navigator.language: {js}) 1234.5 -> \"{1234.5.ToString("N1")}\"");
        }, info: true);

        // Always-correct behaviour regardless of mode.
        Check("ordinal/invariant string ops", () =>
        {
            bool ok = "Hello".ToUpperInvariant() == "HELLO"
                && string.Compare("a", "B", StringComparison.OrdinalIgnoreCase) < 0
                && "ß".ToUpperInvariant() == "ß"
                && "éA".ToLowerInvariant() == "éa"
                && 1.5.ToString(CultureInfo.InvariantCulture) == "1.5"
                && "x́".Normalize(NormalizationForm.FormC).Length >= 1;
            return (ok, $"normalize len={"é".Normalize(NormalizationForm.FormC).Length}");
        });
    }

    // ---------------------------------------------------------------- Exceptions

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowFromHelper(string message) => throw new ArgumentException(message, "param");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Recurse(int n) => n == 0 ? throw new InvalidOperationException("deep") : Recurse(n - 1) + 1;

    private void RunExceptionChecks()
    {
        Check("exceptions nested try/catch/finally", () =>
        {
            var log = new List<string>();
            try
            {
                try
                {
                    try
                    {
                        log.Add("t3");
                        ThrowFromHelper("inner");
                    }
                    finally
                    {
                        log.Add("f3");
                    }
                }
                catch (ArgumentException e) when (e.ParamName == "param")
                {
                    log.Add("c2");
                    throw new InvalidOperationException("outer", e);
                }
                finally
                {
                    log.Add("f2");
                }
            }
            catch (InvalidOperationException e)
            {
                log.Add("c1:" + e.InnerException?.Message.Split(' ')[0]);
            }
            finally
            {
                log.Add("f1");
            }
            string s = string.Join(",", log);
            return (s == "t3,f3,c2,f2,c1:inner,f1", s);
        });

        Check("exceptions filters", () =>
        {
            var log = new List<string>();
            bool Filter(string tag, bool result) { log.Add(tag); return result; }
            try
            {
                try
                {
                    try
                    {
                        throw new FormatException("x");
                    }
                    finally
                    {
                        log.Add("finally");
                    }
                }
                catch (Exception) when (Filter("f1", false))
                {
                    log.Add("wrong");
                }
            }
            catch (FormatException) when (Filter("f2", true))
            {
                log.Add("caught");
            }
            // Two-pass: filters run before the inner finally.
            string s = string.Join(",", log);
            return (s == "f1,f2,finally,caught", s);
        });

        Check("exceptions rethrow + stack trace", () =>
        {
            try
            {
                try
                {
                    Recurse(5);
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
            }
            catch (InvalidOperationException e)
            {
                string st = e.StackTrace ?? "";
                int frames = st.Split('\n').Count(l => l.Contains("Recurse"));
                bool hasLine = st.Contains(":line ");
                string first = st.Split('\n')[0].Trim();
                bool ok = frames == 6 && st.Contains("RunExceptionChecks");
                return (ok, $"Recurse frames={frames} line numbers={hasLine} first=\"{first}\"");
            }
            return (false, "not caught");
        });

        Check("exceptions runtime (null/div/cast/overflow/index)", () =>
        {
            var got = new List<string>();
            object o = "s";
            string n = null;
            int zero = 0;
            int[] arr = new int[2];
            int big = int.MaxValue;
            void T(Action a) { try { a(); got.Add("none"); } catch (Exception e) { got.Add(e.GetType().Name); } }
            T(() => _ = n.Length);
            T(() => _ = 5 / zero);
            T(() => _ = (int)o);
            T(() => _ = checked(big + 1));
            T(() => _ = arr[zero + 2]);
            T(() => _ = 5.0 / zero);
            string s = string.Join(",", got);
            return (s == "NullReferenceException,DivideByZeroException,InvalidCastException,OverflowException,IndexOutOfRangeException,none", s);
        });

        Check("exceptions async/AggregateException", () =>
        {
            var t = System.Threading.Tasks.Task.FromException<int>(new TimeoutException("t"));
            try
            {
                _ = t.Result;
                return (false, "no throw");
            }
            catch (AggregateException e)
            {
                return (e.InnerException is TimeoutException, $"{e.GetType().Name} -> {e.InnerException?.GetType().Name}");
            }
        });

        // Exceptions thrown in C# code invoked by Godot (signals, Callable.Call, Object.Call). The
        // managed trampolines catch and log them; the caller and the other handlers keep running.
        Check("exceptions across Godot signal", () =>
        {
            _signalHandlersRun = 0;
            bool finallyRan = false;
            Callable thrower = Callable.From((int v) =>
            {
                try
                {
                    _signalHandlersRun++;
                    throw new InvalidOperationException("from signal handler (expected, logged by Godot)");
                }
                finally
                {
                    finallyRan = true;
                }
            });
            Callable counter = Callable.From((int v) => { _signalHandlersRun += v; });
            Connect(SignalName.WrtSignal, thrower);
            Connect(SignalName.WrtSignal, counter);
            Error err = EmitSignal(SignalName.WrtSignal, 10);
            Disconnect(SignalName.WrtSignal, thrower);
            Disconnect(SignalName.WrtSignal, counter);
            bool ok = _signalHandlersRun == 11 && finallyRan;
            return (ok, $"EmitSignal={err} handlers sum={_signalHandlersRun} finally={finallyRan}");
        });

        Check("exceptions across Callable.Call / Object.Call", () =>
        {
            int after = 0;
            Variant r1 = Callable.From(() => { throw new NotSupportedException("from Callable (expected, logged by Godot)"); }).Call();
            after++;
            Variant r2 = Call(MethodName.ThrowingMethod);
            after++;
            // A C# -> Godot -> C# chain where the exception is caught on the inner C# side.
            Variant r3 = Callable.From(() =>
            {
                try { ThrowFromHelper("caught inside"); return 1; }
                catch (ArgumentException) { return 2; }
            }).Call();
            bool ok = after == 2 && r1.VariantType == Variant.Type.Nil && r2.VariantType == Variant.Type.Nil && r3.AsInt32() == 2;
            return (ok, $"r1={r1.VariantType} r2={r2.VariantType} r3={r3}");
        });

        Pending("exceptions in deferred call");
        Callable.From(() =>
        {
            _deferredThrowerRan = true;
            throw new InvalidOperationException("from deferred call (expected, logged by Godot)");
        }).CallDeferred();
        Callable.From(() => Complete("exceptions in deferred call", _deferredThrowerRan, "next deferred call ran after the throwing one")).CallDeferred();
    }

    public void ThrowingMethod() => throw new InvalidOperationException("from Object.Call (expected, logged by Godot)");

    // GodotSharp -> engine calls whose wasm signatures aren't used by the BCL (see
    // 'NativeFuncsSignatures.cs' in the GetRuntimePack project). Each used to trap with
    // 'RuntimeError: function signature mismatch', taking the whole page down.
    private void RunNativeSignatureChecks()
    {
        Check("Variant -> String/StringName/NodePath/Rid", () =>
        {
            string str = Variant.From(12.5).AsString();
            StringName sn = Variant.From("name").AsStringName();
            NodePath np = Variant.From("a/b:c").AsNodePath();
            Rid rid = Variant.From(GetCanvasItem()).AsRid();
            bool ok = str == "12.5" && sn == "name" && np.GetNameCount() == 2 && rid == GetCanvasItem() && rid.IsValid;
            return (ok, $"str={str} sn={sn} np={np} rid={rid.Id}");
        });

        Check("Godot.Collections.Array.Resize", () =>
        {
            var arr = new Godot.Collections.Array { 1, 2 };
            Error e = arr.Resize(5);
            return (e == Error.Ok && arr.Count == 5, $"Resize -> {e}, Count={arr.Count}");
        });

        Check("GD.Randf / Color OK HSL", () =>
        {
            float r = GD.Randf();
            var c = Color.FromOkHsl(0.5f, 0.5f, 0.5f);
            float h = c.OkHslH;
            return (r >= 0 && r < 1 && Mathf.Abs(h - 0.5f) < 0.01f, $"randf={r} okhsl_h={h}");
        });

        Check("PackedByteArray Compress/Decompress", () =>
        {
            byte[] data = Encoding.UTF8.GetBytes(new string('z', 1000));
            byte[] comp = data.Compress(FileAccess.CompressionMode.Deflate);
            byte[] back = comp.Decompress(data.Length, FileAccess.CompressionMode.Deflate);
            byte[] dyn = comp.DecompressDynamic(-1, FileAccess.CompressionMode.Deflate);
            return (back.SequenceEqual(data) && dyn.SequenceEqual(data), $"{data.Length} -> {comp.Length} -> {back.Length}/{dyn.Length}");
        });

        Pending("await ToSignal");
        _ = ToSignalCheck();

        Check("engine error print (GD.PushError/PushWarning, ERR_FAIL)", () =>
        {
            GD.PushWarning("WRT expected warning from GD.PushWarning");
            GD.PushError("WRT expected error from GD.PushError");
            var orphan = new Node();
            orphan.RemoveChild(this); // Engine-side ERR_FAIL (expected error).
            orphan.Free();
            return (true, "returned");
        });

    }

    private async System.Threading.Tasks.Task ToSignalCheck()
    {
        var timer = GetTree().CreateTimer(0.05);
        await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
        Complete("await ToSignal", true, "SceneTreeTimer.Timeout awaited");
    }

    // ---------------------------------------------------------------- Time zones

    private void RunTimeZoneChecks()
    {
        Check("TimeZoneInfo.ConvertTime (IANA, DST)", () =>
        {
            var ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
            var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
            var lh = TimeZoneInfo.FindSystemTimeZoneById("Australia/Lord_Howe");
            var summer = new DateTimeOffset(2024, 7, 1, 12, 0, 0, TimeSpan.Zero);
            var winter = new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);
            var nyS = TimeZoneInfo.ConvertTime(summer, ny);
            var nyW = TimeZoneInfo.ConvertTime(winter, ny);
            var beS = TimeZoneInfo.ConvertTime(summer, berlin);
            var tk = TimeZoneInfo.ConvertTime(winter, tokyo);
            var lhW = TimeZoneInfo.ConvertTime(winter, lh); // southern summer: +11
            bool ok = nyS.Hour == 8 && nyW.Hour == 7 && beS.Hour == 14 && tk.Hour == 21 && lhW.Offset == TimeSpan.FromHours(11)
                && ny.IsDaylightSavingTime(summer) && !ny.IsDaylightSavingTime(winter);
            // DST gap: 2024-03-10 02:30 doesn't exist in New York.
            bool invalid = ny.IsInvalidTime(new DateTime(2024, 3, 10, 2, 30, 0));
            ok &= invalid;
            return (ok, $"NY {nyS:HH:mm zzz}/{nyW:HH:mm zzz} Berlin {beS:HH:mm zzz} Tokyo {tk:HH:mm zzz} LordHowe {lhW:zzz} gap={invalid}");
        });

        Check("TimeZoneInfo.GetSystemTimeZones", () =>
        {
            var all = TimeZoneInfo.GetSystemTimeZones();
            bool ok = all.Count > 300 && all.Any(z => z.Id == "Europe/London");
            return (ok, $"count={all.Count} first={all.FirstOrDefault()?.Id}");
        });

        Check("TimeZoneInfo Windows id", () =>
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                bool conv = TimeZoneInfo.TryConvertWindowsIdToIanaId("Eastern Standard Time", out string iana);
                return (tz.BaseUtcOffset == TimeSpan.FromHours(-5), $"Id={tz.Id} TryConvertWindowsIdToIanaId={conv}:{iana}");
            }
            catch (Exception e)
            {
                return (false, $"threw {e.GetType().Name}: {e.Message}");
            }
        }, info: true);

        Check("TimeZoneInfo.Local DST rules", () =>
        {
            var local = TimeZoneInfo.Local;
            var rules = local.GetAdjustmentRules();
            string js = JavaScriptBridge.Eval("String(-new Date(2024, 6, 1).getTimezoneOffset()) + '/' + String(-new Date(2024, 0, 15).getTimezoneOffset())").AsString();
            var jul = local.GetUtcOffset(new DateTime(2024, 7, 1, 12, 0, 0));
            var jan = local.GetUtcOffset(new DateTime(2024, 1, 15, 12, 0, 0));
            bool ok = js == $"{(int)jul.TotalMinutes}/{(int)jan.TotalMinutes}";
            return (ok, $"{local.Id} rules={rules.Length} C# Jul/Jan offset min={(int)jul.TotalMinutes}/{(int)jan.TotalMinutes} JS={js} DisplayName=\"{local.DisplayName}\" StandardName=\"{local.StandardName}\"");
        });
    }
}
