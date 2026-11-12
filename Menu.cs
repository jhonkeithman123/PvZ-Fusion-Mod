using UnityEngine;

namespace Blooms_Mods
{
    /// <summary>
    /// IMGUI panel drawn from <see cref="Core.OnGUI"/>. Avoids GUI.Window because
    /// GUI.WindowFunction is an Il2Cpp delegate that C# method groups cannot bind to.
    /// </summary>
    public static class Menu
    {
        private const float Width = 360f;
        private const float RowHeight = 20f;
        private const float TitleHeight = 24f;

        private static Rect windowRect = new Rect(20f, 20f, Width, 220f);
        private static bool dragging;
        private static Vector2 dragOffset;

        public static void Render()
        {
            float height = TitleHeight + 6f;
            foreach (BloomsModEntry mod in BloomsMods.Entries)
            {
                if (mod == null)
                    continue;
                height += RowHeight;
                if (mod.Features != null)
                    height += mod.Features.Count * RowHeight;
                height += 6f;
            }
            height += RowHeight;

            Rect rect = new Rect(windowRect.x, windowRect.y, Width, height);

            GUI.Box(rect, string.Empty, GUI.skin.box);

            GUI.Label(new Rect(rect.x + 8f, rect.y + 3f, rect.width - 16f, 20f),
                      "Blooms Mods v1.0.0", GUI.skin.label);

            float y = rect.y + TitleHeight;

            foreach (BloomsModEntry mod in BloomsMods.Entries)
            {
                if (mod == null || mod.Name == null)
                    continue;

                GUI.Label(new Rect(rect.x + 8f, y, rect.width - 16f, 18f),
                          mod.Name + " v" + mod.Version + "  -  " + mod.Author, GUI.skin.label);
                y += RowHeight;

                if (mod.Features == null)
                    continue;

                foreach (BloomsFeature feature in mod.Features)
                {
                    if (feature == null || feature.Name == null)
                        continue;

                    bool value = feature.IsEnabled();
                    bool newValue = GUI.Toggle(new Rect(rect.x + 8f, y, rect.width - 16f, 18f),
                                               value, feature.Name + feature.HotkeyLabel);
                    if (newValue != value)
                        feature.SetEnabled(newValue);
                    y += RowHeight;
                }

                y += 6f;
            }

            GUI.Label(new Rect(rect.x + 8f, y, rect.width - 16f, 18f),
                      "Press F3 to close", GUI.skin.label);

            HandleDrag(rect);
        }

        private static void HandleDrag(Rect rect)
        {
            Event e = Event.current;
            if (e == null)
                return;

            Rect titleRect = new Rect(rect.x, rect.y, rect.width, TitleHeight);

            if (e.type == EventType.MouseDown && titleRect.Contains(e.mousePosition))
            {
                dragging = true;
                dragOffset = new Vector2(e.mousePosition.x - rect.x, e.mousePosition.y - rect.y);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && dragging)
            {
                windowRect.x = e.mousePosition.x - dragOffset.x;
                windowRect.y = e.mousePosition.y - dragOffset.y;
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                dragging = false;
            }
        }
    }
}
