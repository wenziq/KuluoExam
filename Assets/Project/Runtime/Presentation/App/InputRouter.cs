using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Sokoban.Core.Rules;
namespace Sokoban.Runtime.Presentation.App
{
    public enum InputContext
    {
        Gameplay, Page, MenuOrGesture, Text, Modal
    }
    public sealed class InputRouter : MonoBehaviour
    {
        public event Action Cleared;
        public event Action<Direction> MoveRequested;
        public event Action EscapeRequested;
        private readonly Dictionary<object, Action> escapeHandlers = new Dictionary<object, Action>();
        public void SetEscapeHandler(object owner, Action handler) => escapeHandlers[owner] = handler;
        public void RemoveEscapeHandler(object owner) => escapeHandlers.Remove(owner);
        public void DispatchEscape()
        {
            if (Context < InputContext.Modal && TextFocused())
            {
                ClearPending();
                EventSystem.current.SetSelectedGameObject(null);
                return;
            }
            ClearPending();
            object best = null;
            InputContext priority = pageContext;
            foreach (var pair in overlays)
                if (pair.Value >= priority && escapeHandlers.ContainsKey(pair.Key))
                {
                    best = pair.Key;
                    priority = pair.Value;
                }
            if (best != null)
                escapeHandlers[best]();
            else
                EscapeRequested?.Invoke();
        }
        private bool textWasFocused;
        private readonly System.Collections.Generic.List<Direction> pressOrder = new System.Collections.Generic.List<Direction>();
        [Min(.01f)] public float initialRepeatDelay = .25f;
        [Min(.01f)] public float repeatInterval = .12f;
        private InputContext pageContext = InputContext.Page;
        private readonly Dictionary<object, InputContext> overlays = new Dictionary<object, InputContext>();
        private Direction? held;
        private float nextRepeat;
        private bool suppressedUntilRelease;
        public InputContext Context
        {
            get
            {
                var result = pageContext;
                foreach (var v in overlays.Values)
                    if (v > result)
                        result = v;
                return result;
            }
        }
        public bool HasCaptureOtherThan(object owner)
        {
            foreach(var key in overlays.Keys) if(!ReferenceEquals(key,owner)) return true;
            return false;
        }
        public bool CanMove => Context == InputContext.Gameplay && !TextFocused();
        public void SetContext(InputContext context)
        {
            if (pageContext == context)
                return;
            pageContext = context;
            ClearPending();
        }
        public void Capture(object owner, InputContext context)
        {
            overlays[owner] = context;
            ClearPending();
        }
        public void Release(object owner)
        {
            if (overlays.Remove(owner))
                ClearPending();
        }
        public void ClearPending()
        {
            held = null;
            suppressedUntilRelease = true;
            pressOrder.Clear();
            Cleared?.Invoke();
        }
        private static bool TextFocused()
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            return selected != null && selected.GetComponentInParent<TMP_InputField>() != null;
        }
        private void OnApplicationFocus(bool focus)
        {
            ClearPending();
        }
        private void OnDisable()
        {
            ClearPending();
        }
        private void Track(Direction direction, bool pressed, bool newlyPressed)
        {
            if (!pressed)
                pressOrder.Remove(direction);
            else if (newlyPressed)
            {
                pressOrder.Remove(direction);
                pressOrder.Add(direction);
            }
        }
        private void Update()
        {
            var k = Keyboard.current;
            if (k == null)
                return;
            if (k.escapeKey.wasPressedThisFrame)
            {
                DispatchEscape();
                return;
            }
            bool text = TextFocused();
            if (text != textWasFocused)
            {
                textWasFocused = text;
                ClearPending();
            }
            Track(Direction.Up, k.upArrowKey.isPressed || k.wKey.isPressed, k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame);
            Track(Direction.Down, k.downArrowKey.isPressed || k.sKey.isPressed, k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame);
            Track(Direction.Left, k.leftArrowKey.isPressed || k.aKey.isPressed, k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame);
            Track(Direction.Right, k.rightArrowKey.isPressed || k.dKey.isPressed, k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame);
            Direction? direction = pressOrder.Count == 0 ? (Direction?)null : pressOrder[pressOrder.Count - 1];
            bool any = k.upArrowKey.isPressed || k.wKey.isPressed || k.downArrowKey.isPressed || k.sKey.isPressed || k.leftArrowKey.isPressed || k.aKey.isPressed || k.rightArrowKey.isPressed || k.dKey.isPressed;
            if (suppressedUntilRelease && any)
                return;
            if (direction == null)
            {
                suppressedUntilRelease = false;
                held = null;
                return;
            }
            if (!CanMove || suppressedUntilRelease)
            {
                held = null;
                return;
            }
            if (held != direction)
            {
                held = direction;
                nextRepeat = Time.unscaledTime + initialRepeatDelay;
                MoveRequested?.Invoke(direction.Value);
            }
            else if (Time.unscaledTime >= nextRepeat)
            {
                nextRepeat = Time.unscaledTime + repeatInterval;
                MoveRequested?.Invoke(direction.Value);
            }
        }
    }
}
