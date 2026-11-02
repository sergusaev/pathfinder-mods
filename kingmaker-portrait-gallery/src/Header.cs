using System.Linq;
using HarmonyLib;
using Kingmaker.UI._ConsoleUI.CharGen.Phases.Portrait;
using Kingmaker.UI._ConsoleUI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PortraitGallery
{
    // The inside of a category bar: a dark fill, the chevron on the left and the name. The frame around it is the
    // portrait cell's own (both interfaces), so a bar has the same two bands as a portrait and lights up the same way.
    // The chevron is KM's gold journal arrow as the save list uses it: 12x18, pointing right while the category is
    // collapsed, turned down while it is open.
    class Header : MonoBehaviour
    {
        static readonly Color Back = new Color(0.10f, 0.08f, 0.06f, 0.85f);
        static readonly Color Gold = new Color(0.86f, 0.74f, 0.50f, 1f);
        static Sprite s_Arrow, s_Drawn;

        Image m_Back;
        RectTransform m_Chevron;
        Image m_ChevronImage;
        TextMeshProUGUI m_Label;
        internal Category Category;

        // KM's gold arrow when loaded, otherwise one drawn like it.
        static Sprite Arrow()
        {
            if (!s_Arrow)
                s_Arrow = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(x => x.name == "journal_arrow_expand_active");
            if (s_Arrow) return s_Arrow;
            if (!s_Drawn)
            {
                const int w = 23, h = 36;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var a = new Vector2(5, 4); var b = new Vector2(18, 18); var c = new Vector2(5, 32);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float d = Mathf.Min(Distance(p, a, b), Distance(p, b, c));
                        tex.SetPixel(x, y, new Color(Gold.r, Gold.g, Gold.b, Mathf.Clamp01(2.6f - d)));
                    }
                tex.Apply();
                s_Drawn = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
                s_Drawn.name = "PortraitGallery_arrow";
            }
            return s_Drawn;
        }

        static float Distance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        internal static RectTransform Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        internal static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        internal static Header Create(Transform parent, TMP_FontAsset font, float fontSize)
        {
            var rt = Child(parent, "PortraitGalleryHeader");
            Stretch(rt);
            var h = rt.gameObject.AddComponent<Header>();
            h.m_Back = rt.gameObject.AddComponent<Image>();
            h.m_Back.color = Back;

            h.m_Chevron = Child(rt, "Chevron");
            h.m_Chevron.anchorMin = h.m_Chevron.anchorMax = new Vector2(0, 0.5f);
            h.m_Chevron.pivot = new Vector2(0.5f, 0.5f);
            h.m_Chevron.anchoredPosition = new Vector2(24, 0);
            h.m_Chevron.sizeDelta = new Vector2(12, 18);
            h.m_ChevronImage = h.m_Chevron.gameObject.AddComponent<Image>();
            h.m_ChevronImage.sprite = Arrow();
            h.m_ChevronImage.raycastTarget = false;

            var lt = Child(rt, "Label");
            h.m_Label = lt.gameObject.AddComponent<TextMeshProUGUI>();
            // TextMeshPro sizes its rect when added: the stretch goes after it.
            Stretch(lt);
            lt.offsetMin = new Vector2(44, 0); lt.offsetMax = new Vector2(-12, 0);
            if (font) h.m_Label.font = font;
            h.m_Label.fontSize = fontSize;
            h.m_Label.fontStyle = FontStyles.Normal;
            h.m_Label.color = Gold;
            h.m_Label.alignment = TextAlignmentOptions.MidlineLeft;
            h.m_Label.enableWordWrapping = false;
            h.m_Label.raycastTarget = false;
            return h;
        }

        internal Image Background { get { return m_Back; } }

        internal void Set(Category category)
        {
            Category = category;
            m_Label.text = Gallery.Name(category);
            m_Chevron.localRotation = Quaternion.Euler(0, 0, Gallery.IsCollapsed(category) ? 0f : -90f);
            Center();
        }

        // The letters themselves in the middle of the bar, whatever the font's line metrics.
        void Center()
        {
            var rt = m_Label.rectTransform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, 0);
            if (!m_Label.gameObject.activeInHierarchy) return;
            m_Label.ForceMeshUpdate();
            var b = m_Label.textBounds;
            if (b.size.y > 0) rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -b.center.y);
        }

        void OnEnable()
        {
            if (m_Label) Center();
        }

        // The font of the interface around, for the label.
        internal static TMP_FontAsset FontNear(Component c)
        {
            var canvas = c.GetComponentInParent<Canvas>();
            var text = canvas ? canvas.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.font) : null;
            return text ? text.font : null;
        }
    }

    // A pooled cell of the gamepad list that can turn into a category bar and back. As a bar it hides only the
    // portrait picture: the frame (Border), the chosen frame (SelectedBorder, the cell's choice mark) and the focus
    // highlight stay and stretch over the bar. A focused bar shows the chosen frame, as the current portrait does.
    class PadCell : MonoBehaviour
    {
        static readonly System.Reflection.FieldInfo SelectedField = AccessTools.Field(typeof(ConsoleSelectableEntity), "m_IsSelected");
        static readonly System.Reflection.FieldInfo PortraitField = AccessTools.Field(typeof(CharGenPortraitSelectorItemView), "m_Portrait");
        static readonly System.Reflection.FieldInfo ChoiceField = AccessTools.Field(typeof(CharGenPortraitSelectorItemView), "m_ChoiceMark");

        Vector2 m_Size;
        Header m_Header;
        GameObject m_Portrait, m_Choice;
        ConsoleSelectableEntity m_Selectable;
        internal bool IsHeader;

        internal static PadCell Get(CharGenPortraitSelectorItemView view)
        {
            var cell = view.GetComponent<PadCell>();
            if (cell) return cell;
            cell = view.gameObject.AddComponent<PadCell>();
            cell.m_Size = ((RectTransform)view.transform).sizeDelta;
            cell.m_Selectable = view.GetComponent<ConsoleSelectableEntity>();
            var portrait = PortraitField.GetValue(view) as Image;
            cell.m_Portrait = portrait ? portrait.gameObject : null;
            cell.m_Choice = ChoiceField.GetValue(view) as GameObject;
            return cell;
        }

        bool Focused { get { return m_Selectable && (bool)SelectedField.GetValue(m_Selectable); } }

        internal void ShowHeader(Category category, Vector2 size)
        {
            if (!m_Header)
            {
                m_Header = Header.Create(transform, Header.FontNear(this), 22);
                m_Header.Background.raycastTarget = false;
            }
            m_Header.transform.SetAsFirstSibling();   // under the frames
            IsHeader = true;
            if (m_Portrait) m_Portrait.SetActive(false);
            ((RectTransform)transform).sizeDelta = size;
            m_Header.gameObject.SetActive(true);
            m_Header.Set(category);
            if (m_Choice) m_Choice.SetActive(Focused);
        }

        internal void ShowPortrait()
        {
            if (!IsHeader) return;
            IsHeader = false;
            if (m_Portrait) m_Portrait.SetActive(true);
            ((RectTransform)transform).sizeDelta = m_Size;
            if (m_Header) m_Header.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (IsHeader && m_Choice && m_Choice.activeSelf != Focused) m_Choice.SetActive(Focused);
        }
    }
}
