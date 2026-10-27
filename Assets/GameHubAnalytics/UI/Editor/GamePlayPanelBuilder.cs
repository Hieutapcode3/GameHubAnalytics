using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GamePlay.Editor
{
    // Builds the uGUI popup prefabs from UI/Art into UI/Resources/UI, where GamePlayUI loads them.
    public static class GamePlayPanelBuilder
    {
        private const string ArtDir  = "Assets/GameHubAnalytics/UI/Art/"; // art taken from the Skyden "Free Casual GUI" pack
        private const string OutDir  = "Assets/GameHubAnalytics/UI/Resources/UI";
        private const int MaxNameLength = 20;

        private static readonly Color Brown = new Color32(0x6B, 0x4A, 0x2B, 0xFF);
        private static Font _font;

        [MenuItem("GameHub/🎨 Build UI Panel Prefabs", priority = 2)]
        public static void Build()
        {
            _font = AssetDatabase.LoadAssetAtPath<Font>(ArtDir + "Baloo-Regular.ttf");
            if (_font == null)
            {
                EditorUtility.DisplayDialog("Thiếu UI Art", $"Không tìm thấy font/sprite trong {ArtDir}.", "OK");
                return;
            }

            Directory.CreateDirectory(OutDir);
            BuildNamePanel();
            BuildRatingPanel();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#2EA3FF>[GamePlay]</color> ✅ Đã tạo NamePanel & RatingPanel prefab trong {OutDir}");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(OutDir + "/NamePanel.prefab"));
        }

        private static void BuildNamePanel()
        {
            var root = CreatePopup("NamePanel", LoadSprite("Shape_01.png"), new Vector2(900, 712), out var panel);

            // the top ~100px of Shape_01 is the avatar icon, so content sits a bit lower than center
            Label("Title", panel, "CHÀO MỪNG!", 68, Brown, new Vector2(0, 165), new Vector2(820, 90));
            Label("Subtitle", panel, "Nhập tên của bạn để bắt đầu:", 40, Brown, new Vector2(0, 85), new Vector2(820, 60));

            var inputBg = Img("NameInput", panel, LoadSprite("Shape_11.png"), new Vector2(0, -30), new Vector2(720, 133));
            var input = inputBg.gameObject.AddComponent<InputField>();
            var text = Label("Text", inputBg.transform, "", 48, Brown, Vector2.zero, new Vector2(660, 110));
            text.supportRichText = false;
            var placeholder = Label("Placeholder", inputBg.transform, "Tên của bạn...", 48, new Color(Brown.r, Brown.g, Brown.b, 0.45f), Vector2.zero, new Vector2(660, 110));
            placeholder.fontStyle = FontStyle.Italic;
            input.targetGraphic  = inputBg;
            input.textComponent  = text;
            input.placeholder    = placeholder;
            input.characterLimit = MaxNameLength;

            var confirm = Btn("ConfirmButton", panel, ButtonSprite("Buttons_07"), "XÁC NHẬN", Color.white, new Vector2(0, -225), new Vector2(520, 183));

            var comp = root.AddComponent<NamePanel>();
            SetRef(comp, "nameInput", input);
            SetRef(comp, "confirmButton", confirm);
            Save(root);
        }

        private static void BuildRatingPanel()
        {
            var root = CreatePopup("RatingPanel", LoadSprite("Shape_10.png"), new Vector2(900, 712), out var panel);

            Label("Title", panel, "ĐÁNH GIÁ MÀN CHƠI", 60, Brown, new Vector2(0, 280), new Vector2(820, 80));
            var question = Label("Question", panel, "Bạn thấy màn này thế nào?", 38, Brown, new Vector2(0, 210), new Vector2(820, 60));

            const float starSize = 120f, starStep = 130f;
            var row = new GameObject("Stars", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(panel, false);
            row.anchoredPosition = new Vector2(0, 100);
            row.sizeDelta = new Vector2(starStep * 5, starSize);
            var starSprite = LoadSprite("Star.png");
            var stars = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                stars[i] = Img($"Star{i + 1}", row, starSprite, new Vector2((i - 2) * starStep, 0), new Vector2(starSize, starSize));
                stars[i].preserveAspect = true;
            }

            var ratingText = Label("RatingText", panel, "", 36, Brown, new Vector2(0, 0), new Vector2(820, 56));
            var submit = Btn("SubmitButton", panel, ButtonSprite("Buttons_07"), "GỬI ĐÁNH GIÁ", Color.white, new Vector2(0, -120), new Vector2(460, 162));
            var skip   = Btn("SkipButton", panel, ButtonSprite("Buttons_013"), "BỎ QUA", Brown, new Vector2(0, -262), new Vector2(340, 120));

            var comp = root.AddComponent<RatingPanel>();
            SetRef(comp, "questionText", question);
            SetRef(comp, "ratingText", ratingText);
            SetRef(comp, "starRow", row);
            SetRef(comp, "submitButton", submit);
            SetRef(comp, "skipButton", skip);
            var so = new SerializedObject(comp);
            var arr = so.FindProperty("stars");
            arr.arraySize = stars.Length;
            for (int i = 0; i < stars.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = stars[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(root);
        }

        // Full-screen overlay canvas with a dark backdrop (blocks clicks behind) and a centered panel.
        private static GameObject CreatePopup(string name, Sprite panelSprite, Vector2 panelSize, out RectTransform panel)
        {
            var root = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = 0f; // width-driven, same as the IMGUI layout

            var backdrop = Img("Backdrop", root.transform, null, Vector2.zero, Vector2.zero);
            backdrop.rectTransform.anchorMin = Vector2.zero;
            backdrop.rectTransform.anchorMax = Vector2.one;
            backdrop.color = new Color(0f, 0f, 0f, 0.82f);

            panel = Img("Panel", root.transform, panelSprite, Vector2.zero, panelSize).rectTransform;
            return root;
        }

        private static Image Img(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            return img;
        }

        private static Text Label(string name, Transform parent, string content, int size, Color color, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var text = go.GetComponent<Text>();
            text.font = _font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Button Btn(string name, Transform parent, Sprite sprite, string label, Color labelColor, Vector2 pos, Vector2 size)
        {
            var img = Img(name, parent, sprite, pos, size);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            // pack button sprites have a drop shadow at the bottom, so lift the label slightly
            Label("Text", img.transform, label, Mathf.RoundToInt(size.y * 0.3f), labelColor, new Vector2(0, size.y * 0.04f), size * 0.85f);
            return btn;
        }

        private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + path);

        private static Sprite ButtonSprite(string name) =>
            AssetDatabase.LoadAllAssetsAtPath(ArtDir + "Buttons.png").OfType<Sprite>().First(s => s.name == name);

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Save(GameObject root)
        {
            PrefabUtility.SaveAsPrefabAsset(root, $"{OutDir}/{root.name}.prefab");
            Object.DestroyImmediate(root);
        }
    }
}
