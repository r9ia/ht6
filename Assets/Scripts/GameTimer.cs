using UnityEngine;

public class GameTimer : MonoBehaviour
{
    private const float FadeDuration = 1.5f;

    private float elapsed;
    private bool escaped;
    private float escapedAt;
    private Texture2D blackTexture;

    private void Awake()
    {
        blackTexture = new Texture2D(1, 1);
        blackTexture.SetPixel(0, 0, Color.black);
        blackTexture.Apply();
    }

    private void Update()
    {
        if (!escaped)
        {
            elapsed += Time.deltaTime;
        }
    }

    public void OnPlayerEscaped()
    {
        if (escaped) return;
        escaped = true;
        escapedAt = Time.time;
    }

    private void OnGUI()
    {
        int minutes = Mathf.FloorToInt(elapsed / 60f);
        int seconds = Mathf.FloorToInt(elapsed % 60f);
        string timeText = $"{minutes:00}:{seconds:00}";

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20;
        style.normal.textColor = Color.white;

        GUI.Label(new Rect(10, 10, 300, 30), $"Time: {timeText}", style);

        if (escaped)
        {
            float fadeT = Mathf.Clamp01((Time.time - escapedAt) / FadeDuration);

            GUI.color = new Color(0f, 0f, 0f, fadeT);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTexture);
            GUI.color = Color.white;

            GUIStyle bigStyle = new GUIStyle(GUI.skin.label);
            bigStyle.fontSize = 40;
            bigStyle.alignment = TextAnchor.MiddleCenter;
            bigStyle.normal.textColor = new Color(1f, 1f, 1f, fadeT);
            GUI.Label(new Rect(Screen.width / 2f - 250, Screen.height / 2f - 25, 500, 50),
                $"You escaped! Time: {timeText}", bigStyle);
        }
    }
}
