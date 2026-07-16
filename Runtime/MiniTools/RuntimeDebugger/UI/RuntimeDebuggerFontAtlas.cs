using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SAS.Utilities.RuntimeDebugger.Core;
using UnityEngine;

namespace SAS.Utilities.RuntimeDebugger
{
    internal sealed class RuntimeDebuggerFontAtlas
    {
        private readonly HashSet<char> _characterSet = new();
        private readonly StringBuilder _characters = new(256);

        internal void RequestRuntimeCharacters(Font font, RuntimeDebuggerController controller)
        {
            if (Event.current.type != EventType.Repaint || font == null || !font.dynamic)
                return;

            _characterSet.Clear();
            _characters.Clear();
            Add("0123456789▶▼ (inactive):");
            Add(controller.Search);
            Add(controller.EditValue);
            Add(controller.Error);

            foreach (RuntimeHierarchyEntry entry in controller.VisibleEntries)
                Add(entry.Name);

            RuntimeObjectDetails details = controller.Details;
            if (details != null)
            {
                Add(details.Name);
                Add(details.Tag);
                Add(details.Layer.ToString(CultureInfo.InvariantCulture));
                foreach (RuntimeComponentDescriptor component in details.Components)
                {
                    Add(component.TypeName);
                    Add(component.StatusMessage);
                    if (component.Members == null)
                        continue;

                    foreach (RuntimeMemberDescriptor member in component.Members)
                    {
                        Add(member.DisplayName);
                        Add(member.Value);
                        Add(member.Error);
                    }
                }
            }

            if (_characters.Length == 0)
                return;

            string characters = _characters.ToString();
            font.RequestCharactersInTexture(characters, 10, FontStyle.Normal);
            font.RequestCharactersInTexture(characters, 11, FontStyle.Normal);
            font.RequestCharactersInTexture(characters, 12, FontStyle.Normal);
            font.RequestCharactersInTexture(characters, 12, FontStyle.Bold);
        }

        private void Add(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            foreach (char character in text)
                if (_characterSet.Add(character))
                    _characters.Append(character);
        }
    }
}
