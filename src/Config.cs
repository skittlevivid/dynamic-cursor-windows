using System;
using System.Globalization;
using System.IO;

namespace DynamicCursors
{
    public enum SimulationMode
    {
        Tilt,
        Rotate,
        Stretch,
        ShakeOnly
    }

    public enum ActivationType
    {
        Linear,
        Quadratic,
        NegativeQuadratic
    }

    public class AppConfig
    {
        public SimulationMode Mode { get; set; }
        public ActivationType Activation { get; set; }

        // Tilt mode settings (air drag)
        public float TiltLimit { get; set; }
        public float TiltFullAngle { get; set; }
        public int TiltWindowMs { get; set; }
        public float TiltSmoothing { get; set; }

        // Rotate mode settings (stick simulation)
        public float RotateStickLength { get; set; }
        public float RotateOffset { get; set; }
        public float RotateSmoothing { get; set; }

        // Stretch mode settings
        public float StretchFactor { get; set; }
        public float StretchLimit { get; set; }
        public float MaxStretch { get; set; }
        public int StretchWindowMs { get; set; }

        // Shake to find settings
        public bool ShakeEnabled { get; set; }
        public float ShakeThreshold { get; set; }
        public float ShakeLimit { get; set; }
        public float ShakeBase { get; set; }
        public float ShakeInfluence { get; set; }
        public int ShakeTimeoutMs { get; set; }

        // General settings
        public float CursorScale { get; set; }
        public bool HideSystemCursor { get; set; }
        public int TargetFPS { get; set; }
        public bool Enabled { get; set; }

        public AppConfig()
        {
            Mode = SimulationMode.Tilt;
            Activation = ActivationType.Quadratic;

            TiltLimit = 3500.0f;
            TiltFullAngle = 28.0f;
            TiltWindowMs = 80;
            TiltSmoothing = 0.35f;

            RotateStickLength = 24.0f;
            RotateOffset = 0.0f;
            RotateSmoothing = 0.40f;

            StretchFactor = 0.0004f;
            StretchLimit = 3000.0f;
            MaxStretch = 1.9f;
            StretchWindowMs = 70;

            ShakeEnabled = true;
            ShakeThreshold = 3.0f;
            ShakeLimit = 4.0f;
            ShakeBase = 1.5f;
            ShakeInfluence = 2.2f;
            ShakeTimeoutMs = 550;

            CursorScale = 1.0f;
            HideSystemCursor = true;
            TargetFPS = 120;
            Enabled = true;
        }

        public static string GetConfigPath()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appDir, "config.ini");
        }

        public void Load()
        {
            string path = GetConfigPath();
            if (!File.Exists(path))
            {
                Save();
                return;
            }

            try
            {
                string[] lines = File.ReadAllLines(path);
                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("["))
                        continue;

                    int idx = line.IndexOf('=');
                    if (idx < 0) continue;

                    string key = line.Substring(0, idx).Trim();
                    string val = line.Substring(idx + 1).Trim();

                    switch (key.ToLowerInvariant())
                    {
                        case "mode":
                            try { Mode = (SimulationMode)Enum.Parse(typeof(SimulationMode), val, true); } catch { }
                            break;
                        case "activation":
                            try { Activation = (ActivationType)Enum.Parse(typeof(ActivationType), val, true); } catch { }
                            break;
                        case "tiltlimit":
                            { float tl; if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out tl)) TiltLimit = tl; }
                            break;
                        case "tiltfullangle":
                            { float tfa; if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out tfa)) TiltFullAngle = tfa; }
                            break;
                        case "tiltwindowms":
                            { int twm; if (int.TryParse(val, out twm)) TiltWindowMs = twm; }
                            break;
                        case "tiltsmoothing":
                            { float ts; if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out ts)) TiltSmoothing = ts; }
                            break;
                        case "rotatesticklength":
                            { float rsl; if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out rsl)) RotateStickLength = rsl; }
                            break;
                        case "rotateoffset":
                            { float ro; if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out ro)) RotateOffset = ro; }
                            break;
                        case "rotatesmoothing":
                            { float rs; if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out rs)) RotateSmoothing = rs; }
                            break;
                        case "stretchfactor":
                            { float sf; if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out sf)) StretchFactor = sf; }
                            break;
                        case "stretchlimit":
                            { float sl; if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out sl)) StretchLimit = sl; }
                            break;
                        case "maxstretch":
                            { float ms; if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out ms)) MaxStretch = ms; }
                            break;
                        case "stretchwindowms":
                            { int swm; if (int.TryParse(val, out swm)) StretchWindowMs = swm; }
                            break;
                        case "shakeenabled":
                            { bool se; if (bool.TryParse(val, out se)) ShakeEnabled = se; }
                            break;
                        case "shakethreshold":
                            { float st; if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out st)) ShakeThreshold = st; }
                            break;
                        case "shakelimit":
                            { float sll; if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out sll)) ShakeLimit = sll; }
                            break;
                        case "shakebase":
                            { float sb; if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out sb)) ShakeBase = sb; }
                            break;
                        case "shakeinfluence":
                            { float si; if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out si)) ShakeInfluence = si; }
                            break;
                        case "shaketimeoutms":
                            { int stm; if (int.TryParse(val, out stm)) ShakeTimeoutMs = Math.Max(100, Math.Min(60000, stm)); }
                            break;
                        case "cursorscale":
                            { float cs; if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out cs)) CursorScale = Math.Max(1.0f, Math.Min(100.0f, cs)); }
                            break;
                        case "hidesystemcursor":
                            { bool hsc; if (bool.TryParse(val, out hsc)) HideSystemCursor = hsc; }
                            break;
                        case "targetfps":
                            { int fps; if (int.TryParse(val, out fps)) TargetFPS = fps; }
                            break;
                        case "enabled":
                            { bool en; if (bool.TryParse(val, out en)) Enabled = en; }
                            break;
                    }
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                string path = GetConfigPath();
                using (StreamWriter sw = new StreamWriter(path, false))
                {
                    sw.WriteLine("; Dynamic Cursors Configuration");
                    sw.WriteLine("; Ported from hypr-dynamic-cursors for Windows");
                    sw.WriteLine();
                    sw.WriteLine("[General]");
                    sw.WriteLine("Enabled=" + Enabled);
                    sw.WriteLine("Mode=" + Mode);
                    sw.WriteLine("Activation=" + Activation);
                    sw.WriteLine("CursorScale=" + CursorScale.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("HideSystemCursor=" + HideSystemCursor);
                    sw.WriteLine("TargetFPS=" + TargetFPS);
                    sw.WriteLine();
                    sw.WriteLine("[Tilt]");
                    sw.WriteLine("TiltLimit=" + TiltLimit.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("TiltFullAngle=" + TiltFullAngle.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("TiltWindowMs=" + TiltWindowMs);
                    sw.WriteLine("TiltSmoothing=" + TiltSmoothing.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine();
                    sw.WriteLine("[Rotate]");
                    sw.WriteLine("RotateStickLength=" + RotateStickLength.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("RotateOffset=" + RotateOffset.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("RotateSmoothing=" + RotateSmoothing.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine();
                    sw.WriteLine("[Stretch]");
                    sw.WriteLine("StretchFactor=" + StretchFactor.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("StretchLimit=" + StretchLimit.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("MaxStretch=" + MaxStretch.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("StretchWindowMs=" + StretchWindowMs);
                    sw.WriteLine();
                    sw.WriteLine("[Shake]");
                    sw.WriteLine("ShakeEnabled=" + ShakeEnabled);
                    sw.WriteLine("ShakeThreshold=" + ShakeThreshold.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("ShakeLimit=" + ShakeLimit.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("ShakeBase=" + ShakeBase.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("ShakeInfluence=" + ShakeInfluence.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("ShakeTimeoutMs=" + ShakeTimeoutMs);
                }
            }
            catch { }
        }
    }
}
