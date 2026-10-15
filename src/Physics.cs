using System;
using System.Collections.Generic;

namespace DynamicCursors
{
    public struct Vector2D
    {
        public float X;
        public float Y;

        public Vector2D(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float Length()
        {
            return (float)Math.Sqrt(X * X + Y * Y);
        }

        public float Distance(Vector2D other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public static Vector2D operator +(Vector2D a, Vector2D b)
        {
            return new Vector2D(a.X + b.X, a.Y + b.Y);
        }

        public static Vector2D operator -(Vector2D a, Vector2D b)
        {
            return new Vector2D(a.X - b.X, a.Y - b.Y);
        }

        public static Vector2D operator *(Vector2D a, float s)
        {
            return new Vector2D(a.X * s, a.Y * s);
        }

        public static Vector2D operator /(Vector2D a, float s)
        {
            if (s == 0f) return a;
            return new Vector2D(a.X / s, a.Y / s);
        }
    }

    public struct ModeResult
    {
        public float Rotation;       // in degrees
        public float Scale;          // shake magnification factor (1.0 = normal)
        public float StretchAngle;   // in degrees
        public float StretchX;       // stretch factor along stretch angle
        public float StretchY;       // squash factor perpendicular
    }

    public struct StretchResult
    {
        public float Angle;
        public float ScaleX;
        public float ScaleY;

        public StretchResult(float angle, float sx, float sy)
        {
            Angle = angle;
            ScaleX = sx;
            ScaleY = sy;
        }
    }

    public static class ActivationCalculator
    {
        public static float Calculate(ActivationType type, float max, float value)
        {
            if (max <= 0.001f) return 0f;
            float result = 0f;
            switch (type)
            {
                case ActivationType.Linear:
                    result = value / max;
                    break;

                case ActivationType.Quadratic:
                    result = (1.0f / (max * max)) * (value * value) * Math.Sign(value);
                    break;

                case ActivationType.NegativeQuadratic:
                    float abs = Math.Abs(value);
                    result = ((-1.0f / (max * max)) * ((abs - max) * (abs - max)) + 1.0f) * Math.Sign(value);
                    if (abs > max) result = Math.Sign(value);
                    break;
            }

            return Math.Max(-1.0f, Math.Min(1.0f, result));
        }
    }

    public class TiltSimulator
    {
        private readonly Queue<KeyValuePair<long, Vector2D>> _history = new Queue<KeyValuePair<long, Vector2D>>();
        private float _currentAngle = 0f;

        public void Reset()
        {
            _history.Clear();
            _currentAngle = 0f;
        }

        public float Update(Vector2D pos, long nowMs, AppConfig config)
        {
            _history.Enqueue(new KeyValuePair<long, Vector2D>(nowMs, pos));

            long cutoff = nowMs - config.TiltWindowMs;
            while (_history.Count > 1 && _history.Peek().Key < cutoff)
            {
                _history.Dequeue();
            }

            if (_history.Count < 2) return _currentAngle;

            KeyValuePair<long, Vector2D> oldest = _history.Peek();
            long dt = nowMs - oldest.Key;
            if (dt <= 0) return _currentAngle;

            float speedX = (pos.X - oldest.Value.X) / (dt / 1000.0f);
            float act = ActivationCalculator.Calculate(config.Activation, config.TiltLimit, speedX);
            float targetAngle = act * config.TiltFullAngle;

            // Smooth interpolation
            float factor = Math.Max(0.05f, Math.Min(1.0f, config.TiltSmoothing));
            _currentAngle += (targetAngle - _currentAngle) * factor;

            return _currentAngle;
        }
    }

    public class RotateSimulator
    {
        private Vector2D _stickEnd = new Vector2D(0, 0);
        private bool _initialized = false;
        private float _currentAngle = 0f;

        public void Reset()
        {
            _initialized = false;
            _stickEnd = new Vector2D(0, 0);
            _currentAngle = 0f;
        }

        public float Update(Vector2D pos, AppConfig config)
        {
            float length = config.RotateStickLength;
            if (length <= 1.0f) length = 24.0f;

            if (!_initialized)
            {
                _stickEnd = new Vector2D(pos.X, pos.Y + length);
                _initialized = true;
            }

            Vector2D rel = _stickEnd - pos;
            float size = rel.Length();
            if (size == 0f) size = 1f;

            rel.X = (rel.X / size) * length;
            rel.Y = (rel.Y / size) * length;

            // Angle in degrees from stick trailing
            float angleRad = (float)-Math.Atan2(rel.X, rel.Y);
            if (rel.Y > 0) angleRad += (float)Math.PI;
            angleRad += (float)Math.PI;

            float deg = angleRad * (180.0f / (float)Math.PI) + config.RotateOffset;

            _stickEnd = pos + rel;

            // Angle difference unwrapping
            float diff = (deg - _currentAngle) % 360f;
            if (diff > 180f) diff -= 360f;
            if (diff < -180f) diff += 360f;

            float factor = Math.Max(0.05f, Math.Min(1.0f, config.RotateSmoothing));
            _currentAngle += diff * factor;

            return _currentAngle;
        }
    }

    public class StretchSimulator
    {
        private readonly Queue<KeyValuePair<long, Vector2D>> _history = new Queue<KeyValuePair<long, Vector2D>>();
        private float _currentAngle = 0f;
        private float _currentStretchX = 1f;
        private float _currentStretchY = 1f;

        public void Reset()
        {
            _history.Clear();
            _currentAngle = 0f;
            _currentStretchX = 1f;
            _currentStretchY = 1f;
        }

        public StretchResult Update(Vector2D pos, long nowMs, AppConfig config)
        {
            _history.Enqueue(new KeyValuePair<long, Vector2D>(nowMs, pos));

            long cutoff = nowMs - config.StretchWindowMs;
            while (_history.Count > 1 && _history.Peek().Key < cutoff)
            {
                _history.Dequeue();
            }

            if (_history.Count < 2)
            {
                return new StretchResult(_currentAngle, _currentStretchX, _currentStretchY);
            }

            KeyValuePair<long, Vector2D> oldest = _history.Peek();
            long dt = nowMs - oldest.Key;
            if (dt <= 0) return new StretchResult(_currentAngle, _currentStretchX, _currentStretchY);

            Vector2D speed = (pos - oldest.Value) / (dt / 1000.0f);
            float mag = speed.Length();

            if (mag > 50.0f)
            {
                _currentAngle = (float)(Math.Atan2(speed.Y, speed.X) * (180.0 / Math.PI));
            }

            float act = ActivationCalculator.Calculate(config.Activation, config.StretchLimit, mag);
            float targetSx = 1.0f + act * (config.MaxStretch - 1.0f);
            float targetSy = 1.0f / targetSx;

            _currentStretchX += (targetSx - _currentStretchX) * 0.35f;
            _currentStretchY += (targetSy - _currentStretchY) * 0.35f;

            return new StretchResult(_currentAngle, _currentStretchX, _currentStretchY);
        }
    }

    public class ShakeDetector
    {
        private readonly List<KeyValuePair<long, Vector2D>> _samples = new List<KeyValuePair<long, Vector2D>>();
        private float _zoom = 1.0f;
        private float _targetZoom = 1.0f;
        private long _timeoutTime = 0;
        private bool _active = false;

        public void Reset()
        {
            _samples.Clear();
            _zoom = 1.0f;
            _targetZoom = 1.0f;
            _active = false;
            _timeoutTime = 0;
        }

        public float Update(Vector2D pos, long nowMs, AppConfig config)
        {
            if (!config.ShakeEnabled) return 1.0f;

            _samples.Add(new KeyValuePair<long, Vector2D>(nowMs, pos));

            long cutoff = nowMs - 800; // 800ms window
            int removeIdx = 0;
            while (removeIdx < _samples.Count && _samples[removeIdx].Key < cutoff)
            {
                removeIdx++;
            }
            if (removeIdx > 0) _samples.RemoveRange(0, removeIdx);

            if (_samples.Count >= 3)
            {
                // Calculate path length
                float trail = 0f;
                for (int i = 1; i < _samples.Count; i++)
                {
                    trail += _samples[i].Value.Distance(_samples[i - 1].Value);
                }

                // Calculate bounding box diagonal
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                for (int i = 0; i < _samples.Count; i++)
                {
                    Vector2D p = _samples[i].Value;
                    if (p.X < minX) minX = p.X;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.Y > maxY) maxY = p.Y;
                }

                float diag = (float)Math.Sqrt((maxX - minX) * (maxX - minX) + (maxY - minY) * (maxY - minY));

                // If shaken back and forth (trail is much larger than diagonal)
                if (diag > 40.0f)
                {
                    float amount = (trail / diag) - config.ShakeThreshold;
                    if (amount > 0f)
                    {
                        if (!_active)
                        {
                            _targetZoom = config.ShakeBase;
                            _active = true;
                        }

                        // Grow punchily based on shake energy
                        _targetZoom += (amount * 0.45f) * config.ShakeInfluence;
                        if (_targetZoom > config.ShakeLimit)
                            _targetZoom = config.ShakeLimit;

                        _timeoutTime = nowMs + config.ShakeTimeoutMs;
                    }
                }
            }

            if (_active && nowMs > _timeoutTime)
            {
                _targetZoom = 1.0f;
                _active = false;
            }

            // Smooth spring lerp: fast zoom-in, smooth glide back
            float lerpSpeed = _active ? 0.35f : 0.16f;
            _zoom += (_targetZoom - _zoom) * lerpSpeed;
            if (Math.Abs(_zoom - 1.0f) < 0.02f && !_active) _zoom = 1.0f;

            return _zoom;
        }
    }
}
