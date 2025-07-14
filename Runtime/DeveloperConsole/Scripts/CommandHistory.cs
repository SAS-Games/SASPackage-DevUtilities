using System.Collections.Generic;

namespace SAS.Utilities.DeveloperConsole
{
    public class CommandHistory
    {
        private readonly List<string> _history = new();
        private int _index = -1;
        private readonly int _maxSize;

        public CommandHistory(int maxSize = 50)
        {
            _maxSize = maxSize;
        }

        public void Add(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            // Avoid duplicates in sequence (optional)
            if (_history.Count > 0 && _history[^1] == command)
                return;

            if (_history.Count >= _maxSize)
                _history.RemoveAt(0);

            _history.Add(command);
            _index = _history.Count; // reset index to end
        }

        public string GetPrevious()
        {
            if (_history.Count == 0 || _index <= 0)
                return string.Empty;

            _index--;
            return _history[_index];
        }

        public string GetNext()
        {
            if (_history.Count == 0 || _index >= _history.Count - 1)
                return string.Empty;

            _index++;
            return _history[_index];
        }

        public void ResetIndex()
        {
            _index = _history.Count;
        }

        public void Clear()
        {
            _history.Clear();
            _index = -1;
        }
    }
}