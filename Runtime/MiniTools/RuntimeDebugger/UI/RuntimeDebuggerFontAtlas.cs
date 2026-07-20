using System.Collections.Generic;
using SAS.Utilities.RuntimeDebugger.Core;
using UnityEngine;

namespace SAS.Utilities.RuntimeDebugger
{
    /// <summary>
    /// Validates that strings used by the runtime debugger exist in the
    /// pre-baked font atlas.
    ///
    /// This class does not generate glyphs at runtime.
    /// </summary>
    internal sealed class RuntimeDebuggerFontAtlas
    {
        private readonly HashSet<int> _reportedMissingCharacters = new();

        /// <summary>
        /// Checks all currently visible debugger text against the assigned fonts.
        /// Only active in Editor and Development builds.
        /// </summary>
        internal void ValidateVisibleCharacters(Font regularFont, Font boldFont, RuntimeDebuggerController controller)
        {
            if (controller == null)
                return;

            Validate(regularFont, "0123456789▶▼↘ (inactive):");

            Validate(regularFont, controller.Search);
            Validate(regularFont, controller.EditValue);
            Validate(regularFont, controller.Error);

            if (controller.VisibleEntries != null)
            {
                foreach (RuntimeHierarchyEntry entry in controller.VisibleEntries)
                    Validate(regularFont, entry.Name);
            }

            RuntimeObjectDetails details = controller.Details;
            if (details != null)
            {
                Validate(regularFont, details.Name);
                Validate(regularFont, details.Tag);
                Validate(regularFont, details.Layer.ToString());

                if (details.Components != null)
                {
                    foreach (RuntimeComponentDescriptor component in details.Components)
                    {
                        Validate(regularFont, component.TypeName);
                        Validate(regularFont, component.StatusMessage);

                        if (component.Members == null)
                            continue;

                        foreach (RuntimeMemberDescriptor member in component.Members)
                        {
                            Validate(regularFont, member.DisplayName);
                            Validate(regularFont, member.Value);
                            Validate(regularFont, member.Error);
                        }
                    }
                }
            }

            Validate(boldFont, "RUNTIME DEBUGGER");
            Validate(boldFont, "LIVE SCENE INSPECTION");
            Validate(boldFont, "SEARCH");
            Validate(boldFont, "HIERARCHY");
            Validate(boldFont, "INSPECTOR");
            Validate(boldFont, "REFRESHING");
            Validate(boldFont, "ITEMS");
            Validate(boldFont, "FOCUS");
            Validate(boldFont, "VIEW");
            Validate(boldFont, "CLEAR");
            Validate(boldFont, "SAVE");
            Validate(boldFont, "EDIT");
            Validate(boldFont, "ACTIVATE");
            Validate(boldFont, "DEACTIVATE");
            Validate(boldFont, "ENABLED");
            Validate(boldFont, "DISABLED");
        }

        /// <summary>
        /// Checks one string against the pre-baked font.
        /// </summary>
        internal void Validate(Font font, string text)
        {
            if (font == null || string.IsNullOrEmpty(text))
                return;

            foreach (char character in text)
            {
                if (char.IsControl(character))
                    continue;

                if (font.HasCharacter(character))
                    continue;

                int missingCharacterKey = MakeCharacterKey(font, character);
                if (!_reportedMissingCharacters.Add(missingCharacterKey))
                    continue;

                Debug.LogWarning(
                    $"[Runtime Debugger] Font '{font.name}' does not contain character '{PrintableCharacter(character)}' U+{(int)character:X4}. Example text: \"{text}\"");
            }
        }

        private static int MakeCharacterKey(Font font, char character)
        {
            return (font != null ? font.GetInstanceID() : 0) * 0x10000 + character;
        }

        private static string PrintableCharacter(char character)
        {
            return character switch
            {
                ' ' => "<space>",
                '\t' => "<tab>",
                '\n' => "<newline>",
                '\r' => "<carriage-return>",
                _ => character.ToString()
            };
        }
    }
}