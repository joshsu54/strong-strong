using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YunJing.Alchemy
{
    // Code-built uGUI in a bamboo-slip palette (original styling, no external UI kit).
    public static class AlchemyUi
    {
        /// Phone layout: taller touch targets and slightly smaller button text so two-column grids fit.
        public static bool Compact;
        public static int ButtonFont => Compact ? 22 : 26;
        public static float ButtonHeight => Compact ? 64 : 58;
        public static readonly Color Bamboo = new Color(.86f, .80f, .62f, .96f);
        public static readonly Color BambooDark = new Color(.55f, .45f, .28f, 1f);
        public static readonly Color Ink = new Color(.16f, .12f, .08f, 1f);
        public static readonly Color Jade = new Color(.36f, .62f, .55f, 1f);
        public static readonly Color Cinnabar = new Color(.72f, .22f, .16f, 1f);
        public static readonly Color Shade = new Color(.08f, .07f, .05f, .55f);
        static Font font;

        // OS CJK fonts: Windows ships JhengHei; Android/iOS names are tried as fallbacks.
        public static Font Font
        {
            get
            {
                if (font == null)
                    font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Microsoft JhengHei UI", "Microsoft YaHei",
                        "Noto Sans CJK TC", "Noto Sans TC", "PingFang TC", "Heiti TC", "Arial Unicode MS" }, 32);
                return font;
            }
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false); rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            return rt;
        }

        public static Image Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var rt = Rect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            var image = rt.gameObject.AddComponent<Image>(); image.color = color; return image;
        }

        /// Bamboo-slip panel: light stalk colour with darker vertical slip seams and two binding cords.
        public static RectTransform Slips(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var bg = Panel(name, parent, anchorMin, anchorMax, offsetMin, offsetMax, Bamboo);
            var seams = Rect("Seams", bg.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var layout = seams.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 0; layout.childForceExpandWidth = true;
            for (int i = 0; i < 14; i++)
            {
                var slip = Panel("Slip", seams, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1, 1, 1, i % 2 == 0 ? .05f : 0));
                var edge = Panel("Edge", slip.transform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-1.5f, 0), Vector2.zero, new Color(.5f, .4f, .22f, .35f));
                edge.raycastTarget = false; slip.raycastTarget = false;
            }
            // Binding cords hug the edges at a fixed inset so text inside the padding never sits on them.
            foreach (var (y, inset) in new[] { (0f, 9f), (1f, -9f) })
                Panel("Cord", bg.transform, new Vector2(0, y), new Vector2(1, y), new Vector2(0, inset - 2), new Vector2(0, inset + 2), new Color(.35f, .22f, .12f, .8f)).raycastTarget = false;
            var frame = bg.gameObject.AddComponent<Outline>(); frame.effectColor = BambooDark; frame.effectDistance = new Vector2(3, -3);
            return (RectTransform)bg.transform;
        }

        public static Text Label(string name, Transform parent, string text, int size, TextAnchor anchor, Color color)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var label = rt.gameObject.AddComponent<Text>(); label.font = Font; label.text = text; label.fontSize = size;
            label.alignment = anchor; label.color = color; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public static Button Button(string name, Transform parent, string text, Action onClick, Color? color = null, int size = 0)
        {
            var image = Panel(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, color ?? Jade);
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors; colors.highlightedColor = new Color(1.1f, 1.1f, 1.05f); colors.pressedColor = new Color(.8f, .8f, .75f);
            colors.disabledColor = new Color(.6f, .6f, .6f, .55f); button.colors = colors;
            button.onClick.AddListener(() => onClick());
            var label = Label("Text", image.transform, text, size > 0 ? size : ButtonFont, TextAnchor.MiddleCenter, Color.white);
            label.GetComponent<RectTransform>().offsetMin = new Vector2(6, 2); label.GetComponent<RectTransform>().offsetMax = new Vector2(-6, -2);
            var le = image.gameObject.AddComponent<LayoutElement>(); le.minHeight = ButtonHeight - 4; le.preferredHeight = ButtonHeight;
            return button;
        }

        public static void SetText(Button button, string text) => button.GetComponentInChildren<Text>().text = text;

        public static VerticalLayoutGroup Column(Transform target, float spacing = 10, int padding = 18)
        {
            var layout = target.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            return layout;
        }

        public static HorizontalLayoutGroup Row(string name, Transform parent, float spacing = 10)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = spacing;
            layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
            var le = rt.gameObject.AddComponent<LayoutElement>(); le.minHeight = ButtonHeight; le.preferredHeight = ButtonHeight; le.flexibleHeight = 0; // force-expand would otherwise soak up spare panel height
            return layout;
        }

        public static Text Line(Transform parent, string text, int size = 24, Color? color = null, int minHeight = 30)
        {
            var label = Label("Line", parent, text, size, TextAnchor.MiddleLeft, color ?? Ink);
            var le = label.gameObject.AddComponent<LayoutElement>(); le.minHeight = minHeight;
            var fitter = label.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return label;
        }

        public static void Clear(Transform target) { for (int i = target.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(target.GetChild(i).gameObject); }

        public static Canvas Canvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            return canvas;
        }
    }

    /// Draggable herb chip used on the tabletop (mouse or touch through the EventSystem).
    public sealed class HerbChip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public string Herb; public Action<HerbChip, Vector2> Dropped; RectTransform rt; Vector2 home; Canvas canvas;
        void Awake() { rt = (RectTransform)transform; canvas = GetComponentInParent<Canvas>(); }
        public void OnBeginDrag(PointerEventData e) { home = rt.anchoredPosition; transform.SetAsLastSibling(); }
        public void OnDrag(PointerEventData e) { rt.anchoredPosition += e.delta / (canvas != null ? canvas.scaleFactor : 1); }
        public void OnEndDrag(PointerEventData e) { var at = e.position; rt.anchoredPosition = home; Dropped?.Invoke(this, at); }
    }

    /// Area that turns circular pointer motion (or taps) into grinding strokes.
    public sealed class GrindPad : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public Action<float> Stroke; Vector2 last; float angle;
        public void OnPointerDown(PointerEventData e) { last = e.position; Stroke?.Invoke(.25f); }
        public void OnDrag(PointerEventData e)
        {
            var center = RectTransformUtility.WorldToScreenPoint(null, transform.position);
            var a = Vector2.SignedAngle(last - center, e.position - center); last = e.position;
            angle += Mathf.Abs(a); while (angle >= 90) { angle -= 90; Stroke?.Invoke(.25f); }
        }
    }
}
