using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints.Classes.Spells;
#if KINGMAKER
using Kingmaker.Blueprints.Console;
#else
using Owlcat.Runtime.UI.ConsoleTools.GamepadInput;
#endif
using Kingmaker.Blueprints.Root;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LearnScrolls
{
    // Who learns what, as a list inside the game's own question window: the character's portrait and name, under it
    // the spells with their icons, the school green when it is the character's specialization and red when it is an
    // opposition school. The window is the game's; the list goes right under its text, in the window's own layout, in
    // the font and the colour of that text, and scrolls when it is long.
    static class LessonList
    {
        const string Name = "LearnScrolls_Lessons";
#if KINGMAKER
        const float MaxHeight = 440f;
#else
        const float MaxHeight = 520f;
#endif
        const float PortraitSize = 48f;
        const float IconSize = 30f;
        // The spells start under the middle of the portrait.
        const float Indent = 32f;
        // The characters a little off the list's edge, their frames whole.
        const float MainIndent = 8f;
        // The spells share one font size, made smaller down to this part of the window's text so that they fit in one
        // line; what does not fit even then wraps.
        const float SpellSize = 0.85f;
        const float MinSpellSize = 0.7f;
        const float StickSpeed = 900f;

        static List<Lesson> s_Pending;
        static string s_Question;
        static int s_AskedFrame;
        static GameObject s_List;
        static ScrollRect s_Scroll;
        static LayoutElement s_Element;
        static int s_BuiltFrame;
        // The window's buttons and their hints: the list must not run over them.
        static readonly List<RectTransform> s_Below = new List<RectTransform>();

        // The list is put into the window that shows the question, as soon as the window is there.
        internal static void Expect(string question, List<Lesson> lessons)
        {
            Close();
            s_Pending = lessons;
            s_Question = question;
            s_AskedFrame = Time.frameCount;
        }

        internal static void Close()
        {
            if (s_List != null) UnityEngine.Object.Destroy(s_List);
            s_List = null;
            s_Scroll = null;
            s_Element = null;
            s_Below.Clear();
            s_Pending = null;
        }

        internal static void Tick()
        {
            if (s_Pending == null) return;
            if (s_List == null)
            {
                if (Time.frameCount - s_AskedFrame > 120)
                {
                    s_Pending = null;
                    return;
                }
                var label = FindLabel();
                if (label != null) s_List = Build(label, s_Pending);
                return;
            }
            if (!s_List.activeInHierarchy)
            {
                Close();
                return;
            }
            Fit();
            if (Game.Instance.IsControllerGamepad) Stick();
        }

        // Some windows (WotR's) keep their buttons where they are whatever the text: the list is made shorter until it
        // ends above them. Where the buttons follow the text (Kingmaker's), nothing overlaps and nothing changes.
        static void Fit()
        {
            // Measured once the window has laid the list out: before that the rule under it may still stand over it.
            if (s_Element == null || s_Below.Count == 0 || Time.frameCount - s_BuiltFrame < 3) return;
            var box = (RectTransform)s_List.transform;
            var corners = new Vector3[4];
            box.GetWorldCorners(corners);
            float top = corners[1].y, bottom = corners[0].y, scale = box.lossyScale.y;
            if (scale <= 0f) return;
            float over = 0f;
            foreach (var rect in s_Below)
            {
                if (rect == null || !rect.gameObject.activeInHierarchy) continue;
                rect.GetWorldCorners(corners);
                float edge = corners[1].y;
                if (edge < top && edge > bottom) over = Mathf.Max(over, (edge - bottom) / scale);
            }
            if (over < 0.5f) return;
            float height = Mathf.Max(MinHeight, s_Element.preferredHeight - over - FitMargin);
            if (Mathf.Abs(height - s_Element.preferredHeight) < 0.5f) return;
            s_Element.preferredHeight = s_Element.minHeight = height;
            box.sizeDelta = new Vector2(box.sizeDelta.x, height);
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)box.parent);
        }

        const float MinHeight = 120f;
        const float FitMargin = 10f;

        // The window's rule over its buttons: a thin wide picture.
        static bool Rule(Component c)
        {
            var image = c as Image;
            if (image == null || image.sprite == null) return false;
            var size = image.rectTransform.rect.size;
            return size.y > 0f && size.y < 40f && size.x > 200f && size.x > size.y * 8f;
        }

        static void FindBelow(Transform label, Transform list)
        {
            s_Below.Clear();
            Transform window = null;
            for (var t = label.parent; t != null; t = t.parent)
                if (t.GetComponents<Component>().Any(c => c != null && (c.GetType().Name.Contains("MessageModal") || c.GetType().Name.Contains("MessageBox"))))
                {
                    window = t;
                    break;
                }
            if (window == null) return;
            foreach (var c in window.GetComponentsInChildren<Component>(true))
            {
                if (c == null || !(c.transform is RectTransform)) continue;
                string type = c.GetType().Name;
                if (c.transform.IsChildOf(list)) continue;
                if (type.Contains("Button") || type.Contains("ConsoleHint") || Rule(c))
                    s_Below.Add((RectTransform)c.transform);
            }
        }

        // The text of the window that shows the question; the game may set its first letter apart.
        static TextMeshProUGUI FindLabel()
        {
            var tail = s_Question.Substring(1);
            return UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>()
                .FirstOrDefault(t => t.isActiveAndEnabled && t.text != null && t.text.Contains(tail) && t.transform.parent != null);
        }

        static GameObject Build(TextMeshProUGUI label, List<Lesson> lessons)
        {
            var parent = label.transform.parent;
            if (parent.GetComponent<LayoutGroup>() == null)
                Main.Log("the question window has no layout under its text: " + parent.name);
            var c = label.color;
            bool paper = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f < 0.5f;
            string good = paper ? "#2F6420" : "#9AD08A", bad = paper ? "#A3241A" : "#E08A7A", muted = paper ? "#6E5743" : "#A8A096";
            // As wide as the window's text column: its layout without the padding.
            var group = parent.GetComponent<LayoutGroup>();
            float width = Mathf.Max(((RectTransform)parent).rect.width - (group != null ? group.padding.horizontal : 0),
                ((RectTransform)label.transform).rect.width);
            if (width < 200f) width = 600f;

            var box = new GameObject(Name, typeof(RectTransform));
            var boxRect = (RectTransform)box.transform;
            boxRect.SetParent(parent, false);
            boxRect.SetSiblingIndex(label.transform.GetSiblingIndex() + 1);
            var boxElement = box.AddComponent<LayoutElement>();
            box.AddComponent<RectMask2D>();
            // Clicks on the list's empty places still scroll it; nothing is drawn behind the list.
            box.AddComponent<Catcher>();
            var scroll = box.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.viewport = boxRect;

            var content = new GameObject("Content", typeof(RectTransform));
            var contentRect = (RectTransform)content.transform;
            contentRect.SetParent(boxRect, false);
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = Vector2.zero;
            var layout = content.AddComponent<VerticalLayoutGroup>();
            // One step between all the lines and the rules; the right side keeps room for the scrollbar.
            layout.spacing = Step;
            layout.padding = new RectOffset(0, (int)ModBar.ModScrollbar.Width, 0, 0);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = contentRect;

            float size = label.fontSize;
            var frame = Frame();
            var rule = parent.Find("Border");
            var spells = new List<TextMeshProUGUI>();
            bool first = true;
            foreach (var unit in lessons.Where(l => l.Unit != null).GroupBy(l => l.Unit))
            {
                if (!first) Rule(contentRect, rule);
                Row(contentRect, label, Portrait(unit.Key), frame, PortraitSize, MainIndent, unit.Key.CharacterName, size, 0f);
                first = false;
                foreach (var lesson in unit.OrderBy(l => l.Level).ThenBy(l => l.Spell.Name))
                    spells.Add(Row(contentRect, label, lesson.Spell.Icon, frame, IconSize, Indent, SpellText(lesson, good, bad, muted), size * SpellSize, 0f, Mark(lesson)));
            }
            var recipes = lessons.Where(l => l.Recipe != null).ToList();
            if (recipes.Count > 0)
            {
                if (!first) Rule(contentRect, rule);
                Row(contentRect, label, null, null, 0f, MainIndent, Strings.RecipesLabel, size, 0f);
                foreach (var lesson in recipes)
                    spells.Add(Row(contentRect, label, lesson.RecipeIcon, frame, IconSize, Indent, lesson.RecipeName, size * SpellSize, 0f));
            }
            // One size for all the spells: the longest one fits in a line, unless that takes a font smaller than the least.
            // The room of the text, the mark at the right end left out.
            float room = width - Indent - IconSize - RowSpacing - ScrollRoom - ModBar.ModScrollbar.Width - (size * SpellSize * 1.6f + RowSpacing);
            float longest = spells.Count > 0 ? spells.Max(t => t.GetPreferredValues(t.text).x) : 0f;
            if (longest > room)
            {
                float fitted = Mathf.Max(size * MinSpellSize, size * SpellSize * room / longest);
                foreach (var t in spells) t.fontSize = fitted;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            boxElement.preferredWidth = boxElement.minWidth = width;
            boxElement.preferredHeight = boxElement.minHeight = Mathf.Min(contentRect.rect.height, MaxHeight);
            // Kingmaker's layout under the text does not size its children: the list sizes itself.
            boxRect.sizeDelta = new Vector2(boxElement.preferredWidth, boxElement.preferredHeight);
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)parent);
            ModBar.ModScrollbar.Follow(scroll, Theme.Find);
            s_Scroll = scroll;
            s_Element = boxElement;
            s_BuiltFrame = Time.frameCount;
            FindBelow(label.transform, boxRect);
            return box;
        }

        // The school in bold; the specialist's school and the opposition schools in colour as well, besides their mark.
        static string SpellText(Lesson lesson, string good, string bad, string muted)
        {
            var text = lesson.Spell.Name + " (" + Strings.SpellLevel(lesson.Level) + ")";
            var school = lesson.Spell.School;
            if (school != SpellSchool.None)
            {
                string name = "<b>" + LocalizedTexts.Instance.SpellSchoolNames.GetText(school) + "</b>";
                string colour = lesson.Book == null ? null
                    : Planner.Opposition(lesson.Book, lesson.Spell) ? bad
                    : Planner.Specialist(lesson.Book, lesson.Spell) ? good : null;
                text += ", " + (colour != null ? "<color=" + colour + ">" + name + "</color>" : name);
            }
            if (lesson.Warning != null) text += " <color=" + muted + ">— " + lesson.Warning + "</color>";
            return text;
        }

        // The school of the specialist and the opposition schools marked as the game marks advised and not advised picks
        // at level up: a green thumb up, a red thumb down.
        static Sprite Mark(Lesson lesson)
        {
            if (lesson.Book == null || lesson.Spell.School == SpellSchool.None) return null;
            if (Planner.Opposition(lesson.Book, lesson.Spell)) return Mark(false);
            if (Planner.Specialist(lesson.Book, lesson.Spell)) return Mark(true);
            return null;
        }

        internal static Sprite Mark(bool like)
        {
#if KINGMAKER
            const string Game = "_km.png";
#else
            const string Game = "_wotr.png";
#endif
            if (like) return s_Like ?? (s_Like = Art.Picture("mark_like" + Game));
            return s_Dislike ?? (s_Dislike = Art.Picture("mark_dislike" + Game));
        }

        static Sprite s_Like, s_Dislike;

        static Sprite Portrait(Kingmaker.EntitySystem.Entities.UnitEntityData unit)
        {
            try
            {
                var data = unit.Descriptor.Portrait;
                return data != null ? data.SmallPortrait : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        const float RowSpacing = 10f;
        // Between the lines of the list and around the rules.
        const float Step = 6f;
        // Room kept at the right for the rounding of the text.
        const float ScrollRoom = 8f;

        static TextMeshProUGUI Row(RectTransform parent, TextMeshProUGUI like, Sprite icon, Sprite frame, float iconSize, float indent,
            string text, float size, float above, Sprite mark = null)
        {
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var row = go.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset((int)indent, 0, (int)above, 0);
            row.spacing = RowSpacing;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            if (icon != null)
            {
                var holder = new GameObject("Icon", typeof(RectTransform));
                holder.transform.SetParent(go.transform, false);
                var element = holder.AddComponent<LayoutElement>();
                // As wide as the picture is at that height: the frame goes round the portrait, not round a square.
                element.minWidth = element.preferredWidth = iconSize * icon.rect.width / Mathf.Max(1f, icon.rect.height);
                element.minHeight = element.preferredHeight = iconSize;
                var image = Child(holder.transform, "Picture", 0f).AddComponent<Image>();
                image.sprite = icon;
                image.preserveAspect = true;
                image.raycastTarget = false;
                if (frame != null)
                {
                    // The frame of the party's portraits laid over the picture's edge.
                    var border = Child(holder.transform, "Frame", 0f).AddComponent<Image>();
                    border.sprite = Thin(frame, iconSize);
                    border.type = frame.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                    border.raycastTarget = false;
                }
            }
            var label = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(go.transform, false);
            label.font = like.font;
            label.fontSharedMaterial = like.fontSharedMaterial;
            label.color = like.color;
            label.fontSize = size;
            label.enableAutoSizing = false;
            label.enableWordWrapping = true;
            label.richText = true;
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.text = text;
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            if (mark != null)
            {
                // At the row's right end, a little taller than the text.
                var holder = new GameObject("Mark", typeof(RectTransform));
                holder.transform.SetParent(go.transform, false);
                var element = holder.AddComponent<LayoutElement>();
                float height = Mathf.Round(size * 1.5f);
                element.minHeight = element.preferredHeight = height;
                element.minWidth = element.preferredWidth = height * mark.rect.width / Mathf.Max(1f, mark.rect.height);
                var image = holder.AddComponent<Image>();
                image.sprite = mark;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            return label;
        }

        static GameObject Child(Transform parent, string name, float outset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-outset, -outset);
            rect.offsetMax = new Vector2(outset, outset);
            return go;
        }

        // The plain frame of the party's portraits at the bottom of the screen (Kingmaker's party bar has it without the
        // health bar's place and the shadow of its usual frame); WotR's party bar frame. Taken from the running game,
        // nothing of it ships with the mod.
#if KINGMAKER
        const string FrameName = "Group_Char_Frame_Normal_Simple";
#else
        const string FrameName = "UI_HudCharacterFrameBorder_Default";
#endif
        static Sprite s_Frame;

        static Sprite Frame()
        {
            if (s_Frame != null) return s_Frame;
            try
            {
                s_Frame = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(sp => sp.name == FrameName) ?? Theme.Find(FrameName);
            }
            catch (Exception e)
            {
                Main.Error(e, "list frame");
            }
            return s_Frame;
        }

        // The frame drawn about as thin as the party bar draws it: its sliced edge a sixteenth of the picture's height.
        static readonly Dictionary<float, Sprite> s_Thin = new Dictionary<float, Sprite>();

        static Sprite Thin(Sprite frame, float size)
        {
            float edge = frame.border.y;
            if (edge <= 0f) return frame;
            Sprite thin;
            if (s_Thin.TryGetValue(size, out thin) && thin != null) return thin;
            float target = Mathf.Max(1.5f, size / 16f);
            thin = Sprite.Create(frame.texture, frame.textureRect, new Vector2(0.5f, 0.5f), 100f * edge / target, 0, SpriteMeshType.FullRect, frame.border);
            s_Thin[size] = thin;
            return thin;
        }

        // The rule between the characters: a copy of the window's own rule over its buttons, else the tooltip rule.
        static void Rule(RectTransform parent, Transform native)
        {
            if (native != null)
            {
                // As wide as in the window, in the middle of the list.
                var size = ((RectTransform)native).rect.size;
                var holder = new GameObject("Rule", typeof(RectTransform));
                holder.transform.SetParent(parent, false);
                var element = holder.AddComponent<LayoutElement>();
                element.minHeight = element.preferredHeight = Mathf.Max(size.y, 8f);
                var copy = UnityEngine.Object.Instantiate(native.gameObject, holder.transform, false);
                copy.SetActive(true);
                var own = copy.GetComponent<LayoutElement>();
                if (own != null) own.ignoreLayout = true;
                var rect = (RectTransform)copy.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                rect.anchoredPosition = Vector2.zero;
                return;
            }
            Theme.Separator(parent);
        }

        // Takes the clicks over the list without drawing anything.
        class Catcher : Graphic
        {
            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
            }
        }

        // The game's question window does not scroll by itself: with a gamepad the right stick scrolls the list.
        static void Stick()
        {
            if (s_Scroll == null) return;
            float y = Rewired.ReInput.players.GetPlayer(0).GetAxis((int)RewiredActionType.RightStickY);
            if (Mathf.Abs(y) < 0.2f) return;
            float hidden = s_Scroll.content.rect.height - s_Scroll.viewport.rect.height;
            if (hidden <= 0f) return;
            s_Scroll.verticalNormalizedPosition = Mathf.Clamp01(s_Scroll.verticalNormalizedPosition + y * StickSpeed * Time.unscaledDeltaTime / hidden);
        }
    }
}
