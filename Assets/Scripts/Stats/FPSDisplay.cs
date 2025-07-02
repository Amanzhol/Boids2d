using UnityEngine;

namespace Stats
{
    public class FPSDisplay : MonoBehaviour {
        float deltaTime = 0.0f;

        void Update() {
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        }

        void OnGUI() {
            int w = Screen.width, h = Screen.height;
            GUIStyle style = new GUIStyle();
            Rect rect = new Rect(-20, 15, w, h * 2 / 100);
            style.alignment = TextAnchor.UpperRight;
            style.fontSize = h * 2 / 50;
            style.normal.textColor = Color.yellow;
            float fps = 1.0f / deltaTime;
            string text = $"FPS: {fps:F1}";
            GUI.Label(rect, text, style);
        }
    }
}