using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SAS.Utilities.RuntimeDebugger.Core
{
    public interface IRuntimeValueDrawer
    {
        bool CanDraw(Type type);
        string Format(object value, Type type);
        bool TryParse(string text, Type type, out object value, out string error);
    }

    public interface IRuntimeComponentDrawer
    {
        bool CanDraw(Type componentType);
        IReadOnlyList<RuntimeMemberDescriptor> BuildInspector(Component component);
    }

    public sealed class RuntimeValueDrawerRegistry
    {
        private readonly List<IRuntimeValueDrawer> _drawers = new();

        public RuntimeValueDrawerRegistry()
        {
            _drawers.Add(new RuntimeScalarDrawer());
            _drawers.Add(new RuntimeStructDrawer());
            _drawers.Add(new RuntimeObjectReferenceDrawer());
        }

        public void Register(IRuntimeValueDrawer drawer)
        {
            if (drawer != null) _drawers.Insert(0, drawer);
        }

        public IRuntimeValueDrawer Resolve(Type type) => _drawers.Find(drawer => drawer.CanDraw(type));
    }

    internal sealed class RuntimeScalarDrawer : IRuntimeValueDrawer
    {
        public bool CanDraw(Type t) => t == typeof(bool) || t == typeof(string) || t.IsEnum || t == typeof(byte) ||
                                       t == typeof(short) || t == typeof(int) || t == typeof(long) ||
                                       t == typeof(float) || t == typeof(double) || t == typeof(LayerMask);

        public string Format(object value, Type type) => type == typeof(LayerMask)
            ? ((LayerMask)value).value.ToString(CultureInfo.InvariantCulture)
            : Convert.ToString(value, CultureInfo.InvariantCulture);

        public bool TryParse(string text, Type type, out object value, out string error)
        {
            try
            {
                if (type == typeof(string)) value = text;
                else if (type == typeof(bool)) value = bool.Parse(text);
                else if (type.IsEnum) value = Enum.Parse(type, text, true);
                else if (type == typeof(LayerMask)) value = (LayerMask)int.Parse(text, CultureInfo.InvariantCulture);
                else value = Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                value = null;
                error = ex.Message;
                return false;
            }
        }
    }

    internal sealed class RuntimeStructDrawer : IRuntimeValueDrawer
    {
        public bool CanDraw(Type t) => t == typeof(Vector2) || t == typeof(Vector2Int) || t == typeof(Vector3) ||
                                       t == typeof(Vector3Int) || t == typeof(Vector4) || t == typeof(Quaternion) ||
                                       t == typeof(Color) || t == typeof(Color32) || t == typeof(Rect) ||
                                       t == typeof(Bounds);

        public string Format(object value, Type t)
        {
            if (value is Vector2 v2) return Join(v2.x, v2.y);
            if (value is Vector2Int v2i) return Join(v2i.x, v2i.y);
            if (value is Vector3 v3) return Join(v3.x, v3.y, v3.z);
            if (value is Vector3Int v3i) return Join(v3i.x, v3i.y, v3i.z);
            if (value is Vector4 v4) return Join(v4.x, v4.y, v4.z, v4.w);
            if (value is Quaternion q)
            {
                Vector3 e = q.eulerAngles;
                return Join(e.x, e.y, e.z);
            }

            if (value is Color c) return Join(c.r, c.g, c.b, c.a);
            if (value is Color32 c32) return Join(c32.r, c32.g, c32.b, c32.a);
            if (value is Rect r) return Join(r.x, r.y, r.width, r.height);
            if (value is Bounds b) return Join(b.center.x, b.center.y, b.center.z, b.size.x, b.size.y, b.size.z);
            return value?.ToString() ?? "null";
        }

        public bool TryParse(string text, Type t, out object value, out string error)
        {
            try
            {
                string[] s = text.Split(',');
                float[] f = Array.ConvertAll(s, x => float.Parse(x.Trim(), CultureInfo.InvariantCulture));
                if (t == typeof(Vector2) && f.Length == 2) value = new Vector2(f[0], f[1]);
                else if (t == typeof(Vector2Int) && f.Length == 2) value = new Vector2Int((int)f[0], (int)f[1]);
                else if (t == typeof(Vector3) && f.Length == 3) value = new Vector3(f[0], f[1], f[2]);
                else if (t == typeof(Vector3Int) && f.Length == 3)
                    value = new Vector3Int((int)f[0], (int)f[1], (int)f[2]);
                else if (t == typeof(Vector4) && f.Length == 4) value = new Vector4(f[0], f[1], f[2], f[3]);
                else if (t == typeof(Quaternion) && f.Length == 3) value = Quaternion.Euler(f[0], f[1], f[2]);
                else if (t == typeof(Color) && f.Length == 4) value = new Color(f[0], f[1], f[2], f[3]);
                else if (t == typeof(Color32) && f.Length == 4)
                    value = new Color32((byte)f[0], (byte)f[1], (byte)f[2], (byte)f[3]);
                else if (t == typeof(Rect) && f.Length == 4) value = new Rect(f[0], f[1], f[2], f[3]);
                else if (t == typeof(Bounds) && f.Length == 6)
                    value = new Bounds(new Vector3(f[0], f[1], f[2]), new Vector3(f[3], f[4], f[5]));
                else throw new FormatException("Unexpected component count.");
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                value = null;
                error = ex.Message;
                return false;
            }
        }

        private static string Join(params object[] values) => string.Join(", ", values);
    }

    internal sealed class RuntimeObjectReferenceDrawer : IRuntimeValueDrawer
    {
        public bool CanDraw(Type type) => typeof(Object).IsAssignableFrom(type);

        public string Format(object value, Type type) => value is Object obj && obj != null
            ? $"{obj.name} ({obj.GetInstanceID()})"
            : "null";

        public bool TryParse(string text, Type type, out object value, out string error)
        {
            value = null;
            error = "Object references are read-only.";
            return false;
        }
    }
}