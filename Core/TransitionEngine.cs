using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace VibranceZ.Core
{
    public struct ColorState
    {
        public int Saturation;
        public int FilterSaturation;
        public int Contrast;
        public int Brightness;
        public int Gamma;
        public int ColorTemp;

        public bool EqualsValues(ColorState other)
        {
            return Saturation == other.Saturation &&
                   FilterSaturation == other.FilterSaturation &&
                   Contrast == other.Contrast &&
                   Brightness == other.Brightness &&
                   Gamma == other.Gamma &&
                   ColorTemp == other.ColorTemp;
        }
    }

    // Debounce ve yumuşak renk geçişi motoru
    public sealed class TransitionEngine : IDisposable
    {
        private readonly Timer _debounceTimer;
        private readonly Timer _animTimer;
        private readonly Stopwatch _sw = new Stopwatch();

        private Action _pendingDebounceAction;
        private ColorState _startState;
        private ColorState _currentState;
        private ColorState _targetState;
        private int _durationMs;
        private bool _hasCurrentState;
        private Action<ColorState> _applyCallback;

        public TransitionEngine()
        {
            _debounceTimer = new Timer();
            _debounceTimer.Tick += OnDebounceTick;

            _animTimer = new Timer { Interval = 16 };
            _animTimer.Tick += OnAnimTick;
        }

        public void ScheduleDebounced(int delayMs, Action action)
        {
            _debounceTimer.Stop();
            if (delayMs <= 0)
            {
                if (action != null) action();
                return;
            }

            _pendingDebounceAction = action;
            _debounceTimer.Interval = delayMs;
            _debounceTimer.Start();
        }

        public void CancelPendingDebounce()
        {
            _debounceTimer.Stop();
            _pendingDebounceAction = null;
        }

        private void OnDebounceTick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            var act = _pendingDebounceAction;
            _pendingDebounceAction = null;
            if (act != null) act();
        }

        public void TransitionTo(ColorState target, int durationMs, Action<ColorState> applyCallback)
        {
            _applyCallback = applyCallback;
            _targetState = target;

            if (!_hasCurrentState || durationMs <= 16 || _currentState.EqualsValues(target))
            {
                _animTimer.Stop();
                _currentState = target;
                _hasCurrentState = true;
                if (applyCallback != null) applyCallback(target);
                return;
            }

            _startState = _currentState;
            _durationMs = durationMs;
            _sw.Restart();
            if (!_animTimer.Enabled)
                _animTimer.Start();
        }

        public void ApplyImmediate(ColorState target, Action<ColorState> applyCallback)
        {
            _animTimer.Stop();
            _currentState = target;
            _targetState = target;
            _hasCurrentState = true;
            _applyCallback = applyCallback;
            if (applyCallback != null) applyCallback(target);
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            double t = Math.Min(1.0, _sw.Elapsed.TotalMilliseconds / Math.Max(1, _durationMs));
            // Cubic ease-out
            double ease = 1.0 - Math.Pow(1.0 - t, 3.0);

            var next = new ColorState
            {
                Saturation = Lerp(_startState.Saturation, _targetState.Saturation, ease),
                FilterSaturation = Lerp(_startState.FilterSaturation, _targetState.FilterSaturation, ease),
                Contrast = Lerp(_startState.Contrast, _targetState.Contrast, ease),
                Brightness = Lerp(_startState.Brightness, _targetState.Brightness, ease),
                Gamma = Lerp(_startState.Gamma, _targetState.Gamma, ease),
                ColorTemp = Lerp(_startState.ColorTemp, _targetState.ColorTemp, ease)
            };

            _currentState = next;
            if (_applyCallback != null)
                _applyCallback(next);

            if (t >= 1.0)
            {
                _animTimer.Stop();
                _sw.Stop();
            }
        }

        private static int Lerp(int a, int b, double t)
        {
            return (int)Math.Round(a + (b - a) * t);
        }

        public void Dispose()
        {
            _debounceTimer.Stop();
            _debounceTimer.Dispose();
            _animTimer.Stop();
            _animTimer.Dispose();
        }
    }
}
