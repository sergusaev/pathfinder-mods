using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Root;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Kingmaker.UI.Common.Animations;
using Kingmaker.UI.LevelUp;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PortraitGallery
{
    // Keyboard and mouse interface. CharBPortraitSelector shows the built-in portraits as toggles in one grid
    // (m_ItemsContainer, a GridLayoutGroup inside a vertical ScrollRect) and appends every custom portrait to it,
    // loading all three images of each (LoadAllPortraitsSmoothly); its upload button loads them all again at once,
    // which hangs the game with a big Portraits folder. The gallery builds the toggles itself: the container becomes
    // a column of category bars, each followed by a grid of its portraits; only the small image of a portrait in
    // sight is loaded (MouseGallery.Update); the upload panel is not offered (portraits are picked from the folders).
    [HarmonyPatch(typeof(CharBPortraitSelector), nameof(CharBPortraitSelector.Init))]
    static class MouseInit
    {
        static readonly F<Transform> ItemsContainer = new F<Transform>("m_ItemsContainer");
        static readonly F<FadeAnimator> ItemsAnimator = new F<FadeAnimator>("m_ItemsContainerAnimator");
        static readonly F<CharBPortraitSelectorItem> ItemPrefab = new F<CharBPortraitSelectorItem>("m_PortraitSlectorItem");
        static readonly F<ToggleGroup> Group = new F<ToggleGroup>("m_ToggleGroup");
        static readonly F<List<CharBPortraitSelectorItem>> Items = new F<List<CharBPortraitSelectorItem>>("m_SelectorItems");
        static readonly F<TextMeshProUGUI> Label = new F<TextMeshProUGUI>("m_Label");
        static readonly F<bool> IsInit = new F<bool>("m_IsInit");
        static readonly F<Toggle> Upload = new F<Toggle>("m_ActivateUploadButton");
        static readonly F<Button> Refresh = new F<Button>("m_RefreshButton");
        static readonly F<Button> ShowFolder = new F<Button>("m_ShowFolderButton");
        static readonly F<FadeAnimator> UploadDescription = new F<FadeAnimator>("m_UploadDescription");

        static bool Prefix(CharBPortraitSelector __instance)
        {
            if (!ItemPrefab.Get(__instance) || !ItemsContainer.Get(__instance)) return true;   // the game logs the broken prefab
            if (IsInit.Get(__instance)) return false;
            try
            {
                EventBus.Subscribe(__instance);
                Label.Get(__instance).text = Game.Instance.BlueprintRoot.LocalizedTexts.UserInterfacesText.CharGen.Portrait;
                var gallery = MouseGallery.Get(__instance, ItemsContainer.Get(__instance), ItemPrefab.Get(__instance), Group.Get(__instance));
                var controller = Game.Instance.UI.CharacterBuildController.LevelUpController;
                Items.Set(__instance, gallery.Build(Gallery.Build(controller)));

                var items = ItemsAnimator.Get(__instance);
                items.gameObject.SetActive(true);
                items.AppearAnimation();
                var upload = UploadDescription.Get(__instance);
                upload.gameObject.SetActive(false);
                items.Initialize();
                upload.Initialize();
                Upload.Get(__instance).onValueChanged.RemoveAllListeners();
                Upload.Get(__instance).gameObject.SetActive(false);
                Refresh.Get(__instance).onClick.RemoveAllListeners();
                Refresh.Get(__instance).gameObject.SetActive(false);
                var showFolder = AccessTools.Method(typeof(CharBPortraitSelector), "ShowPortraitsFolder");
                ShowFolder.Get(__instance).onClick.RemoveAllListeners();
                ShowFolder.Get(__instance).onClick.AddListener(() => showFolder.Invoke(__instance, null));
                IsInit.Set(__instance, true);
                return false;
            }
            catch (Exception e)
            {
                Main.Log.Error("mouse list: " + e);
                return false;
            }
        }
    }

    // The game's Clear destroys the container's children while still holding the toggles; the gallery keeps them.
    [HarmonyPatch(typeof(CharBPortraitSelector), nameof(CharBPortraitSelector.Clear))]
    static class MouseClear
    {
        static bool Prefix(CharBPortraitSelector __instance)
        {
            __instance.Init();
            return false;
        }
    }

    // The frame of a mouse bar: a copy of the portrait toggle stretched over the bar with its picture off, so the bar
    // has the portrait's frame; under the mouse it shows the chosen look, as the current portrait does. The copy's
    // toggle is off: clicks go to the bar's button.
    class MouseBarFrame : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        CharBPortraitSelectorItem m_Item;

        internal void Dress(CharBPortraitSelectorItem prefab)
        {
            m_Item = Instantiate(prefab, transform, false);
            m_Item.name = "PortraitGalleryFrame";
            var rt = (RectTransform)m_Item.transform;
            Header.Stretch(rt);
            var le = m_Item.GetComponent<LayoutElement>();
            if (le) le.ignoreLayout = true;
            if (m_Item.Image)
            {
                if (m_Item.Image.gameObject == m_Item.gameObject) m_Item.Image.enabled = false;
                else m_Item.Image.gameObject.SetActive(false);
            }
            var t = m_Item.Toggle;
            if (t)
            {
                t.onValueChanged.RemoveAllListeners();
                t.group = null;
                t.isOn = false;
                t.enabled = false;
            }
            m_Item.gameObject.SetActive(true);
        }

        void Lit(bool on)
        {
            if (!m_Item || !m_Item.Toggle) return;
            m_Item.Toggle.isOn = on;
            if (m_Item.Toggle.graphic) m_Item.Toggle.graphic.canvasRenderer.SetAlpha(on ? 1f : 0f);
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { Lit(true); }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { Lit(false); }
        void OnDisable() { Lit(false); }
    }

    // A private field of CharBPortraitSelector.
    class F<T>
    {
        readonly System.Reflection.FieldInfo m_Field;
        public F(string name) { m_Field = AccessTools.Field(typeof(CharBPortraitSelector), name); }
        public T Get(CharBPortraitSelector s) { return (T)m_Field.GetValue(s); }
        public void Set(CharBPortraitSelector s, T value) { m_Field.SetValue(s, value); }
    }

    class MouseGallery : MonoBehaviour
    {
        static readonly F<bool> IgnoreOnChange = new F<bool>("m_IgnoreOnChange");
        static readonly F<List<CharBPortraitSelectorItem>> Items = new F<List<CharBPortraitSelectorItem>>("m_SelectorItems");
        static readonly Action<CharBPortraitSelectorItem, bool> SetIsCustom =
            (Action<CharBPortraitSelectorItem, bool>)Delegate.CreateDelegate(typeof(Action<CharBPortraitSelectorItem, bool>),
                AccessTools.PropertySetter(typeof(CharBPortraitSelectorItem), nameof(CharBPortraitSelectorItem.IsCustomPortrait)));
        static readonly Action<CharBPortraitSelectorItem, bool> SetIsSelector =
            (Action<CharBPortraitSelectorItem, bool>)Delegate.CreateDelegate(typeof(Action<CharBPortraitSelectorItem, bool>),
                AccessTools.PropertySetter(typeof(CharBPortraitSelectorItem), nameof(CharBPortraitSelectorItem.IsCustomPortraitSelector)));
        static readonly Vector3[] Corners = new Vector3[4];

        CharBPortraitSelector m_Selector;
        Transform m_Container;
        CharBPortraitSelectorItem m_Prefab;
        ToggleGroup m_Group;
        ScrollRect m_Scroll;
        GridLayoutGroup m_GridTemplate;
        readonly Header[] m_Headers = new Header[Gallery.Count];
        readonly RectTransform[] m_Grids = new RectTransform[Gallery.Count];
        readonly List<CharBPortraitSelectorItem> m_Pool = new List<CharBPortraitSelectorItem>();
        readonly List<CharBPortraitSelectorItem> m_Pending = new List<CharBPortraitSelectorItem>();
        readonly int[] m_Counts = new int[Gallery.Count];
        int m_Frame;

        internal static MouseGallery Get(CharBPortraitSelector selector, Transform container, CharBPortraitSelectorItem prefab, ToggleGroup group)
        {
            var g = selector.GetComponent<MouseGallery>();
            if (!g) g = selector.gameObject.AddComponent<MouseGallery>();
            g.m_Selector = selector;
            g.m_Container = container;
            g.m_Prefab = prefab;
            g.m_Group = group;
            g.m_Scroll = container.GetComponentInParent<ScrollRect>();
            g.KeepScrollbar();
            return g;
        }

        // The scrollbar as in the gamepad list: always shown (a full-height handle when everything fits), and its
        // handle never thinner than a few of its own frames, however long the list (1800+ custom portraits made it
        // a few pixels long, and it vanished).
        void KeepScrollbar()
        {
            if (!m_Scroll || !m_Scroll.verticalScrollbar) return;
            var bar = m_Scroll.verticalScrollbar;
            if (m_Scroll.verticalScrollbarVisibility != ScrollRect.ScrollbarVisibility.Permanent)
            {
                Main.Log.Log("mouse scrollbar: " + m_Scroll.verticalScrollbarVisibility + " -> Permanent");
                m_Scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }
            if (!bar.gameObject.activeSelf) bar.gameObject.SetActive(true);
        }

        float MinHandle()
        {
            var bar = m_Scroll.verticalScrollbar;
            var track = bar.handleRect ? bar.handleRect.parent as RectTransform : null;
            float height = track ? track.rect.height : 0;
            if (height <= 0) return 0;
            float px = 48;
            var img = bar.handleRect.GetComponent<Image>();
            if (img && img.sprite) px = Mathf.Max(px, (img.sprite.border.y + img.sprite.border.w) / img.pixelsPerUnit + 16);
            return Mathf.Min(1f, px / height);
        }

        // After the ScrollRect has set the size for the frame, just before the canvas is drawn.
        void ClampHandle()
        {
            if (!m_Scroll || !m_Scroll.verticalScrollbar || !isActiveAndEnabled) return;
            var bar = m_Scroll.verticalScrollbar;
            if (!bar.gameObject.activeSelf) bar.gameObject.SetActive(true);
            float min = MinHandle();
            if (bar.size < min) bar.size = min;
        }

        void OnEnable() { Canvas.willRenderCanvases += ClampHandle; }
        void OnDisable() { Canvas.willRenderCanvases -= ClampHandle; }

        // The container turns from a grid into a column: bar, grid, bar, grid... The cell size and spacing of the
        // game's grid go to the category grids.
        void Restructure()
        {
            var grid = m_Container.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                var keep = new GameObject("PortraitGalleryGridTemplate", typeof(RectTransform));
                keep.SetActive(false);
                keep.transform.SetParent(transform, false);
                m_GridTemplate = keep.AddComponent<GridLayoutGroup>();
                m_GridTemplate.cellSize = grid.cellSize;
                m_GridTemplate.spacing = grid.spacing;
                m_GridTemplate.constraint = grid.constraint;
                m_GridTemplate.constraintCount = grid.constraintCount;
                m_GridTemplate.childAlignment = grid.childAlignment;
                m_GridTemplate.startAxis = grid.startAxis;
                m_GridTemplate.startCorner = grid.startCorner;
                var padding = grid.padding;
                DestroyImmediate(grid);
                var column = m_Container.gameObject.AddComponent<VerticalLayoutGroup>();
                column.padding = new RectOffset(padding.left, padding.right, padding.top, padding.bottom);
                column.spacing = m_GridTemplate.spacing.y;   // as between portrait rows: the frames stick out of the cells
                column.childAlignment = TextAnchor.UpperLeft;
                column.childControlWidth = true;
                column.childControlHeight = true;
                column.childForceExpandWidth = true;
                column.childForceExpandHeight = false;
            }
            var font = Header.FontNear(m_Container);
            for (int i = 0; i < Gallery.Count; i++)
            {
                if (!m_Headers[i])
                {
                    var holder = new GameObject("PortraitGalleryBar" + i, typeof(RectTransform));
                    holder.layer = m_Container.gameObject.layer;
                    holder.transform.SetParent(m_Container, false);
                    var layout = holder.AddComponent<LayoutElement>();
                    layout.minHeight = layout.preferredHeight = 44;
                    var header = Header.Create(holder.transform, font, 20);
                    var button = holder.AddComponent<Button>();
                    button.targetGraphic = header.Background;
                    button.transition = Selectable.Transition.None;
                    holder.AddComponent<MouseBarFrame>().Dress(m_Prefab);
                    var category = (Category)i;
                    button.onClick.AddListener(() => Toggle(category));
                    m_Headers[i] = header;
                }
                if (!m_Grids[i])
                {
                    var go = new GameObject("PortraitGalleryGrid" + i, typeof(RectTransform));
                    go.layer = m_Container.gameObject.layer;
                    go.transform.SetParent(m_Container, false);
                    var g = go.AddComponent<GridLayoutGroup>();
                    if (m_GridTemplate)
                    {
                        g.cellSize = m_GridTemplate.cellSize;
                        g.spacing = m_GridTemplate.spacing;
                        g.constraint = m_GridTemplate.constraint;
                        g.constraintCount = m_GridTemplate.constraintCount;
                        g.childAlignment = m_GridTemplate.childAlignment;
                        g.startAxis = m_GridTemplate.startAxis;
                        g.startCorner = m_GridTemplate.startCorner;
                    }
                    else
                    {
                        g.cellSize = new Vector2(90, 120);
                        g.spacing = new Vector2(20, 20);
                        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                        g.constraintCount = 4;
                    }
                    m_Grids[i] = (RectTransform)go.transform;
                }
                m_Headers[i].transform.parent.SetSiblingIndex(2 * i);
                m_Grids[i].SetSiblingIndex(2 * i + 1);
            }
        }

        // The toggles for the list, in its order; the game's code finds them by index in m_SelectorItems.
        internal List<CharBPortraitSelectorItem> Build(List<Entry> entries)
        {
            Restructure();
            m_Pool.RemoveAll(i => !i);
            m_Pending.Clear();
            var items = new List<CharBPortraitSelectorItem>();
            var counts = m_Counts;
            Array.Clear(counts, 0, counts.Length);
            var custom = BlueprintRoot.Instance.CharGen.CustomPortrait;
            var blank = BlueprintRoot.Instance.CharGen.BasePortraitSmall;
            for (int n = 0; n < entries.Count; n++)
            {
                var e = entries[n];
                CharBPortraitSelectorItem item;
                if (n < m_Pool.Count) item = m_Pool[n];
                else
                {
                    item = WidgetFactory.GetWidget(m_Prefab);
                    m_Pool.Add(item);
                }
                item.transform.SetParent(m_Grids[(int)e.Category], false);
                item.transform.SetAsLastSibling();
                item.gameObject.SetActive(true);
                item.Toggle.onValueChanged.RemoveAllListeners();
                item.Toggle.group = null;
                item.Toggle.isOn = false;
                if (e.Blueprint != null)
                    item.Init(e.Blueprint, n);
                else
                {
                    item.Portrait = custom;
                    item.PortraitData = Gallery.Data(e.CustomId);
                    item.Index = n;
                    SetIsCustom(item, true);
                    SetIsSelector(item, false);
                    item.Image.sprite = blank;
                    m_Pending.Add(item);
                }
                int index = n;
                var selector = m_Selector;
                item.Toggle.onValueChanged.AddListener(_ => selector.HandleChoosePortrait(index));
                item.Toggle.group = m_Group;
                item.RefreshColor();
                items.Add(item);
                counts[(int)e.Category]++;
            }
            for (int n = entries.Count; n < m_Pool.Count; n++)
            {
                m_Pool[n].Toggle.onValueChanged.RemoveAllListeners();
                m_Pool[n].Toggle.group = null;
                m_Pool[n].gameObject.SetActive(false);
            }
            for (int i = 0; i < Gallery.Count; i++)
            {
                bool any = counts[i] > 0;
                m_Headers[i].transform.parent.gameObject.SetActive(any);
                m_Headers[i].Set((Category)i);
                m_Grids[i].gameObject.SetActive(any && !Gallery.IsCollapsed((Category)i));
            }
            return items;
        }

        void Toggle(Category category)
        {
            Gallery.Toggle(category);
            int i = (int)category;
            m_Headers[i].Set(category);
            // Hiding or showing toggles may make the group switch one on: the chosen portrait stays as it was.
            IgnoreOnChange.Set(m_Selector, true);
            try
            {
                m_Grids[i].gameObject.SetActive(!Gallery.IsCollapsed(category));
                var items = Items.Get(m_Selector);
                if (items != null)
                    for (int n = 0; n < items.Count; n++)
                    {
                        items[n].Toggle.isOn = n == m_Selector.CurrentPortraitIndex;
                        items[n].RefreshColor();
                    }
            }
            finally { IgnoreOnChange.Set(m_Selector, false); }
            Game.Instance.UI.Common.UISound.Play(UISoundType.ButtonClick);
        }

        // Small images of the portraits in sight (and a row around), a few per frame.
        void Update()
        {
            if (m_Pending.Count == 0 || !m_Scroll || ++m_Frame % 2 != 0) return;
            var view = m_Scroll.viewport ? m_Scroll.viewport : (RectTransform)m_Scroll.transform;
            view.GetWorldCorners(Corners);
            float bottom = Corners[0].y, top = Corners[1].y, margin = (top - bottom) * 0.3f;
            bottom -= margin; top += margin;
            int loaded = 0;
            for (int n = m_Pending.Count - 1; n >= 0 && loaded < 6; n--)
            {
                var item = m_Pending[n];
                if (!item || item.PortraitData == null) { m_Pending.RemoveAt(n); continue; }
                if (!item.gameObject.activeInHierarchy) continue;
                ((RectTransform)item.transform).GetWorldCorners(Corners);
                if (Corners[1].y < bottom || Corners[0].y > top) continue;
                item.Image.sprite = item.PortraitData.SmallPortrait;
                m_Pending.RemoveAt(n);
                loaded++;
            }
        }
    }
}
