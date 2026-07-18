using UnityEngine;

public class ExitTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<CharacterController>() == null) return;

        GameTimer timer = FindFirstObjectByType<GameTimer>();
        if (timer != null)
        {
            timer.OnPlayerEscaped();
        }
    }
}
