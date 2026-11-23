using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hushwater
{
    // Keyboard & mouse plus a gamepad, mapped as in the design doc's controls table (Xbox layout):
    // left stick move, right stick look, L3 sprint, B crouch, A interact, RB hold breath, Y crank, LT raise camera,
    // RT take photo, X toggle flash, LB throw, D-pad tools, View journal, Menu pause. R3 turns the lantern up or down.
    // Uses the legacy input manager; the "Pad ..." axes are defined in ProjectSettings/InputManager.asset
    // (Windows numbering for an Xbox pad).
    public static class GameInput
    {
        private static readonly HashSet<string> MissingAxes = new HashSet<string>();
        private static int rtFrame = -1;
        private static bool rtDown;
        private static float rtLast;
        private static int dpadFrame = -1;
        private static int dpadStep;
        private static float dpadLast;

        // The gamepad button that does the same job as a key, or None.
        private static KeyCode Pad(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.E: return KeyCode.JoystickButton0;
                case KeyCode.LeftControl:
                case KeyCode.C: return KeyCode.JoystickButton1;
                case KeyCode.R: return KeyCode.JoystickButton2;
                case KeyCode.F: return KeyCode.JoystickButton3;
                case KeyCode.Q: return KeyCode.JoystickButton4;
                case KeyCode.Space: return KeyCode.JoystickButton5;
                case KeyCode.Tab: return KeyCode.JoystickButton6;
                case KeyCode.Escape: return KeyCode.JoystickButton7;
                case KeyCode.LeftShift: return KeyCode.JoystickButton8;
                case KeyCode.L: return KeyCode.JoystickButton9;
                default: return KeyCode.None;
            }
        }

        public static bool Held(KeyCode key)
        {
            KeyCode pad = Pad(key);
            return Input.GetKey(key) || (pad != KeyCode.None && Input.GetKey(pad));
        }

        public static bool Down(KeyCode key)
        {
            KeyCode pad = Pad(key);
            return Input.GetKeyDown(key) || (pad != KeyCode.None && Input.GetKeyDown(pad));
        }

        // Right mouse or the left trigger.
        public static bool CameraHeld => Input.GetMouseButton(1) || Axis("Pad LT") > 0.4f;

        // Left mouse or a pull of the right trigger, once per press.
        public static bool ShutterDown
        {
            get
            {
                if (Input.GetMouseButtonDown(0)) return true;
                if (rtFrame != Time.frameCount)
                {
                    rtFrame = Time.frameCount;
                    float rt = Axis("Pad RT");
                    rtDown = rt > 0.5f && rtLast <= 0.5f;
                    rtLast = rt;
                }
                return rtDown;
            }
        }

        // Mouse movement plus the right stick (scaled to feel like the mouse at 60 fps).
        public static Vector2 Look
        {
            get
            {
                Vector2 mouse = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                Vector2 stick = new Vector2(Axis("Pad Look X"), -Axis("Pad Look Y")) * (Time.deltaTime * 60f * 1.6f);
                return mouse + stick;
            }
        }

        public static Vector2 Move => Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);

        // -1 / +1 the frame the mouse wheel or the D-pad steps through the tools, otherwise 0.
        public static int ToolStep
        {
            get
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll > 0.01f) return -1;
                if (scroll < -0.01f) return 1;
                if (dpadFrame != Time.frameCount)
                {
                    dpadFrame = Time.frameCount;
                    float x = Axis("Pad DPad X");
                    dpadStep = Mathf.Abs(x) > 0.5f && Mathf.Abs(dpadLast) <= 0.5f ? (int)Mathf.Sign(x) : 0;
                    dpadLast = x;
                }
                return dpadStep;
            }
        }

        // Reads an axis, quietly returning 0 if the project's input settings don't define it.
        private static float Axis(string name)
        {
            if (MissingAxes.Contains(name)) return 0f;
            try
            {
                return Input.GetAxis(name);
            }
            catch (ArgumentException)
            {
                MissingAxes.Add(name);
                return 0f;
            }
        }
    }
}
