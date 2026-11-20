using System;
using System.Collections.Generic;

namespace VibranceZ.Core
{
    public sealed class ColorUndoSnapshot
    {
        public string TargetKey { get; set; } // "DESKTOP" veya profile.Id
        public int Saturation { get; set; }
        public int FilterSaturation { get; set; }
        public int Contrast { get; set; }
        public int Brightness { get; set; }
        public int Gamma { get; set; }
        public int ColorTemp { get; set; }
    }

    // Renk ayarları için geri al / yinele geçmişi
    public sealed class UndoRedoManager
    {
        private const int MaxHistory = 40;
        private readonly List<ColorUndoSnapshot> _undoStack = new List<ColorUndoSnapshot>();
        private readonly List<ColorUndoSnapshot> _redoStack = new List<ColorUndoSnapshot>();

        public bool CanUndo { get { return _undoStack.Count > 0; } }
        public bool CanRedo { get { return _redoStack.Count > 0; } }

        public void PushUndo(ColorUndoSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (_undoStack.Count > 0)
            {
                var top = _undoStack[_undoStack.Count - 1];
                if (top.TargetKey == snapshot.TargetKey &&
                    top.Saturation == snapshot.Saturation &&
                    top.FilterSaturation == snapshot.FilterSaturation &&
                    top.Contrast == snapshot.Contrast &&
                    top.Brightness == snapshot.Brightness &&
                    top.Gamma == snapshot.Gamma &&
                    top.ColorTemp == snapshot.ColorTemp)
                {
                    return;
                }
            }

            _undoStack.Add(snapshot);
            if (_undoStack.Count > MaxHistory)
                _undoStack.RemoveAt(0);
            _redoStack.Clear();
        }

        public ColorUndoSnapshot PopUndo(ColorUndoSnapshot currentForRedo)
        {
            if (_undoStack.Count == 0) return null;
            var item = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            if (currentForRedo != null)
                _redoStack.Add(currentForRedo);
            return item;
        }

        public ColorUndoSnapshot PopRedo(ColorUndoSnapshot currentForUndo)
        {
            if (_redoStack.Count == 0) return null;
            var item = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            if (currentForUndo != null)
                _undoStack.Add(currentForUndo);
            return item;
        }
    }
}
