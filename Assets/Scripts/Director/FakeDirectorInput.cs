using DreadDirector.Network;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DreadDirector.Director
{
    /// <summary>Hackathon demo controls. Every key sends the same messages used by the UDP receiver.</summary>
    public sealed class FakeDirectorInput : MonoBehaviour
    {
        public DirectorGameBridge Bridge;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || Bridge == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                Bridge.ReceiveMessage(DirectorMessage.State(0.38f, 0.12f, 0.18f), "Fake keyboard 1");
                Bridge.ReceiveMessage(DirectorMessage.Event("escalate", 0.35f), "Fake keyboard 1");
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                Bridge.ReceiveMessage(DirectorMessage.State(0.84f, 0.62f, 0.71f), "Fake keyboard 2");
                Bridge.ReceiveMessage(DirectorMessage.Event("escalate", 0.9f), "Fake keyboard 2");
            }
            else if (keyboard.digit3Key.wasPressedThisFrame)
            {
                Bridge.ReceiveMessage(DirectorMessage.Event("panic", 1f), "Fake keyboard 3");
            }
            else if (keyboard.digit4Key.wasPressedThisFrame)
            {
                Bridge.ReceiveMessage(DirectorMessage.State(0.16f, 0.08f, -0.42f), "Fake keyboard 4");
                Bridge.ReceiveMessage(DirectorMessage.Event("recovery", 0f), "Fake keyboard 4");
            }
        }
    }
}
