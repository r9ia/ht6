using UnityEngine;

public class GameTimer : MonoBehaviour
{
    private float elapsed;
    private bool escaped;

    private void Update()
    {
        if (!escaped)
        {
            elapsed += Time.deltaTime;
        }
    }

    public void OnPlayerEscaped()
    {
        escaped = true;
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
            GUIStyle bigStyle = new GUIStyle(GUI.skin.label);
            bigStyle.fontSize = 40;
            bigStyle.alignment = TextAnchor.MiddleCenter;
            bigStyle.normal.textColor = Color.green;
            GUI.Label(new Rect(Screen.width / 2f - 250, Screen.height / 2f - 25, 500, 50),
                $"You escaped! Time: {timeText}", bigStyle);
        }
    }
}
