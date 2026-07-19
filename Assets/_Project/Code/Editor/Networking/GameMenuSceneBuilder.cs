using Cinemachine;
using Odyssey.Networking;
using Odyssey.Systems;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Odyssey.Editor.Networking
{
    /// <summary>
    /// 在 Level_01 中生成唯一的全屏 uGUI 菜单，并把全部控件显式注入 GameMenuController。
    /// 采用编辑器 Builder 集中处理重复的 RectTransform 装配；运行时仍只有一个菜单控制器，不引入 UI 框架。
    /// </summary>
    internal static class GameMenuSceneBuilder
    {
        private static readonly Color PageColor = new Color(0.025f, 0.04f, 0.07f, 0.96f);
        private static readonly Color PanelColor = new Color(0.07f, 0.1f, 0.15f, 0.96f);
        private static readonly Color ButtonColor = new Color(0.12f, 0.34f, 0.5f, 1f);
        private static readonly Color ButtonHighlightColor = new Color(0.18f, 0.48f, 0.68f, 1f);
        private static readonly Color TextColor = new Color(0.92f, 0.96f, 1f, 1f);

        public static GameMenuController Build(
            GameObject runtimeRoot,
            GameplaySessionController session,
            GameplayLocalViewBinder binder,
            SaveManager saveManager)
        {
            RemoveLegacyMenu();
            ConfigureEventSystem();

            var canvasObject = CreateUiObject("游戏菜单", runtimeRoot.transform);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            Stretch(canvasObject.GetComponent<RectTransform>());

            var main = CreatePage(canvasObject.transform, "主菜单页", "ODYSSEY", "单人冒险 / 双人局域网合作");
            var single = CreateButton(main.Content, "单人游戏");
            var network = CreateButton(main.Content, "联机游戏");
            var mainSettings = CreateButton(main.Content, "设置");
            var mainQuit = CreateButton(main.Content, "退出游戏");

            var networkPage = CreatePage(canvasObject.transform, "联机菜单页", "联机游戏", "最多两人合作清关");
            var address = CreateInputField(networkPage.Content, "IP 地址", "127.0.0.1");
            var host = CreateButton(networkPage.Content, "创建房间");
            var client = CreateButton(networkPage.Content, "加入房间");
            var networkStatus = CreateText(networkPage.Content, "联机状态", 20, TextAnchor.MiddleCenter, 86f);
            networkStatus.color = new Color(0.75f, 0.88f, 1f, 1f);
            var networkBack = CreateButton(networkPage.Content, "返回");

            var pause = CreatePage(canvasObject.transform, "ESC 菜单页", "游戏暂停", "");
            var resume = CreateButton(pause.Content, "继续游戏");
            var pauseSettings = CreateButton(pause.Content, "设置");
            var save = CreateButton(pause.Content, "保存游戏");
            var load = CreateButton(pause.Content, "读取存档");
            var leave = CreateButton(pause.Content, "返回主菜单");
            var leaveLabel = leave.GetComponentInChildren<Text>(true);
            var pauseQuit = CreateButton(pause.Content, "退出游戏");
            var pauseStatus = CreateText(pause.Content, "", 20, TextAnchor.MiddleCenter, 42f);
            pauseStatus.color = new Color(0.65f, 0.95f, 0.72f, 1f);

            var settings = CreatePage(canvasObject.transform, "设置页", "设置", "修改后立即生效");
            CreateText(settings.Content, "主音量", 24, TextAnchor.MiddleLeft, 34f);
            var volume = CreateSlider(settings.Content, 0f, 1f, 1f);
            CreateText(settings.Content, "镜头灵敏度", 24, TextAnchor.MiddleLeft, 34f);
            var sensitivity = CreateSlider(settings.Content, 0.2f, 2f, 1f);
            var fullscreen = CreateToggle(settings.Content, "全屏显示");
            var settingsBack = CreateButton(settings.Content, "返回");

            var diagnostics = CreateDiagnostics(canvasObject.transform, out var diagnosticsText);
            diagnostics.SetActive(false);

            var controller = runtimeRoot.AddComponent<GameMenuController>();
            SetReference(controller, "session", session);
            SetReference(controller, "localViewBinder", binder);
            SetReference(controller, "saveManager", saveManager);
            SetReference(controller, "mainMenuPage", main.Root);
            SetReference(controller, "networkMenuPage", networkPage.Root);
            SetReference(controller, "pauseMenuPage", pause.Root);
            SetReference(controller, "settingsPage", settings.Root);
            SetReference(controller, "diagnosticsPanel", diagnostics);
            SetReference(controller, "singlePlayerButton", single);
            SetReference(controller, "networkButton", network);
            SetReference(controller, "mainSettingsButton", mainSettings);
            SetReference(controller, "mainQuitButton", mainQuit);
            SetReference(controller, "hostButton", host);
            SetReference(controller, "clientButton", client);
            SetReference(controller, "networkBackButton", networkBack);
            SetReference(controller, "addressInput", address);
            SetReference(controller, "networkStatusText", networkStatus);
            SetReference(controller, "resumeButton", resume);
            SetReference(controller, "pauseSettingsButton", pauseSettings);
            SetReference(controller, "saveButton", save);
            SetReference(controller, "loadButton", load);
            SetReference(controller, "leaveButton", leave);
            SetReference(controller, "leaveButtonText", leaveLabel);
            SetReference(controller, "pauseQuitButton", pauseQuit);
            SetReference(controller, "pauseStatusText", pauseStatus);
            SetReference(controller, "volumeSlider", volume);
            SetReference(controller, "sensitivitySlider", sensitivity);
            SetReference(controller, "fullscreenToggle", fullscreen);
            SetReference(controller, "settingsBackButton", settingsBack);
            SetReference(controller, "diagnosticsText", diagnosticsText);

            networkPage.Root.SetActive(false);
            pause.Root.SetActive(false);
            settings.Root.SetActive(false);
            return controller;
        }

        private static void RemoveLegacyMenu()
        {
            foreach (var name in new[] { "PauseCanvas", "游戏菜单" })
            {
                var existing = GameObject.Find(name);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing);
                }
            }
        }

        private static void ConfigureEventSystem()
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (eventSystem == null)
            {
                var gameObject = new GameObject("EventSystem");
                eventSystem = gameObject.AddComponent<EventSystem>();
            }

            var legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                Object.DestroyImmediate(legacyModule);
            }

            var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>() ??
                              eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
            EditorUtility.SetDirty(eventSystem.gameObject);
        }

        private static Page CreatePage(Transform parent, string name, string title, string subtitle)
        {
            var root = CreateUiObject(name, parent);
            Stretch(root.GetComponent<RectTransform>());
            var background = root.AddComponent<Image>();
            background.color = PageColor;

            var content = CreateUiObject("内容", root.transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(560f, 760f);
            contentRect.anchoredPosition = Vector2.zero;
            var panel = content.AddComponent<Image>();
            panel.color = PanelColor;
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(52, 52, 44, 44);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var titleText = CreateText(content.transform, title, 42, TextAnchor.MiddleCenter, 66f);
            titleText.fontStyle = FontStyle.Bold;
            if (!string.IsNullOrEmpty(subtitle))
            {
                var subtitleText = CreateText(content.transform, subtitle, 20, TextAnchor.MiddleCenter, 42f);
                subtitleText.color = new Color(0.65f, 0.78f, 0.88f, 1f);
            }

            return new Page(root, content.transform);
        }

        private static Button CreateButton(Transform parent, string label)
        {
            var root = CreateUiObject($"按钮_{label}", parent);
            var image = root.AddComponent<Image>();
            image.color = ButtonColor;
            var button = root.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = ButtonHighlightColor;
            colors.selectedColor = ButtonHighlightColor;
            colors.pressedColor = new Color(0.08f, 0.24f, 0.36f, 1f);
            button.colors = colors;
            SetPreferredHeight(root, 56f);

            var text = CreateText(root.transform, label, 25, TextAnchor.MiddleCenter, 56f);
            Stretch(text.rectTransform);
            return button;
        }

        private static InputField CreateInputField(Transform parent, string label, string defaultValue)
        {
            CreateText(parent, label, 22, TextAnchor.MiddleLeft, 32f);
            var root = CreateUiObject("输入_IP地址", parent);
            var image = root.AddComponent<Image>();
            image.color = new Color(0.9f, 0.94f, 0.98f, 1f);
            var input = root.AddComponent<InputField>();
            SetPreferredHeight(root, 54f);

            var placeholder = CreateText(root.transform, "请输入房主 IPv4", 22, TextAnchor.MiddleLeft, 54f);
            placeholder.color = new Color(0.35f, 0.4f, 0.45f, 0.7f);
            SetInset(placeholder.rectTransform, 16f, 16f, 0f, 0f);
            var text = CreateText(root.transform, defaultValue, 22, TextAnchor.MiddleLeft, 54f);
            text.color = new Color(0.06f, 0.08f, 0.1f, 1f);
            SetInset(text.rectTransform, 16f, 16f, 0f, 0f);
            input.textComponent = text;
            input.placeholder = placeholder;
            input.text = defaultValue;
            input.contentType = InputField.ContentType.Standard;
            return input;
        }

        private static Slider CreateSlider(Transform parent, float minimum, float maximum, float value)
        {
            var root = CreateUiObject("滑动条", parent);
            SetPreferredHeight(root, 42f);
            var slider = root.AddComponent<Slider>();
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.value = value;

            var background = CreateUiObject("背景", root.transform);
            var backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.35f);
            backgroundRect.anchorMax = new Vector2(1f, 0.65f);
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            background.AddComponent<Image>().color = new Color(0.18f, 0.23f, 0.28f, 1f);

            var fillArea = CreateUiObject("填充区域", root.transform);
            SetInset(fillArea.GetComponent<RectTransform>(), 10f, 10f, 0f, 0f);
            var fill = CreateUiObject("填充", fillArea.transform);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0.25f);
            fillRect.anchorMax = new Vector2(1f, 0.75f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.16f, 0.58f, 0.82f, 1f);
            slider.fillRect = fillRect;

            var handleArea = CreateUiObject("滑块区域", root.transform);
            SetInset(handleArea.GetComponent<RectTransform>(), 12f, 12f, 0f, 0f);
            var handle = CreateUiObject("滑块", handleArea.transform);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(28f, 28f);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = TextColor;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            return slider;
        }

        private static Toggle CreateToggle(Transform parent, string label)
        {
            var root = CreateUiObject($"开关_{label}", parent);
            SetPreferredHeight(root, 54f);
            var toggle = root.AddComponent<Toggle>();

            var background = CreateUiObject("背景", root.transform);
            var backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = backgroundRect.anchorMax = new Vector2(0f, 0.5f);
            backgroundRect.pivot = new Vector2(0f, 0.5f);
            backgroundRect.anchoredPosition = Vector2.zero;
            backgroundRect.sizeDelta = new Vector2(36f, 36f);
            var backgroundImage = background.AddComponent<Image>();
            backgroundImage.color = new Color(0.2f, 0.26f, 0.32f, 1f);

            var checkmark = CreateUiObject("选中标记", background.transform);
            var checkmarkRect = checkmark.GetComponent<RectTransform>();
            SetInset(checkmarkRect, 7f, 7f, 7f, 7f);
            var checkmarkImage = checkmark.AddComponent<Image>();
            checkmarkImage.color = new Color(0.2f, 0.72f, 0.95f, 1f);

            var text = CreateText(root.transform, label, 24, TextAnchor.MiddleLeft, 54f);
            SetInset(text.rectTransform, 52f, 0f, 0f, 0f);
            toggle.targetGraphic = backgroundImage;
            toggle.graphic = checkmarkImage;
            toggle.isOn = Screen.fullScreen;
            return toggle;
        }

        private static GameObject CreateDiagnostics(Transform parent, out Text text)
        {
            var root = CreateUiObject("网络调试面板", parent);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(390f, 220f);
            root.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.88f);
            text = CreateText(root.transform, "", 19, TextAnchor.UpperLeft, 200f);
            SetInset(text.rectTransform, 20f, 20f, 16f, 16f);
            return root;
        }

        private static Text CreateText(
            Transform parent,
            string content,
            int fontSize,
            TextAnchor alignment,
            float height)
        {
            var root = CreateUiObject(string.IsNullOrEmpty(content) ? "提示文字" : $"文字_{content}", parent);
            var text = root.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = TextColor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            SetPreferredHeight(root, height);
            return text;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = 5;
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void SetPreferredHeight(GameObject gameObject, float height)
        {
            var layout = gameObject.GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetInset(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetReference(Object target, string fieldName, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(fieldName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private readonly struct Page
        {
            public Page(GameObject root, Transform content)
            {
                Root = root;
                Content = content;
            }

            public GameObject Root { get; }
            public Transform Content { get; }
        }
    }
}
