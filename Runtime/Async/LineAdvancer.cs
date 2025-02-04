using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Yarn.Unity;
using Yarn.Unity.Attributes;

#nullable enable

namespace Yarn.Unity
{
    /// <summary>
    /// A dialogue view that listens for user input and sends requests to a <see
    /// cref="DialogueRunner"/> to advance the presentation of the current line,
    /// either by asking a dialogue runner to hurry up its delivery, advance to
    /// the next line, or cancel the entire dialogue session.
    /// </summary>
    public class LineAdvancer : AsyncDialogueViewBase
    {
        [MustNotBeNull]
        [Tooltip("The dialogue runner that will receive requests to advance or cancel content.")]
        [SerializeField] DialogueRunner? runner;

        /// <summary>
        /// If <see langword="true"/>, repeatedly signalling that the line
        /// should be hurried up will cause the line advancer to request that
        /// the next line be shown.
        /// </summary>
        /// <seealso cref="advanceRequestsBeforeCancellingLine"/>
        [Space]
        [Tooltip("Does repeatedly requesting a line advance cancel the line?")]
        public bool multiAdvanceIsCancel = false;

        /// <summary>
        /// The number of times that a 'hurry up' signal occurs before the line
        /// advancer requests that the next line be shown.
        /// </summary>
        /// <seealso cref="multiAdvanceIsCancel"/>
        [ShowIf(nameof(multiAdvanceIsCancel))]
        [Indent]
        [Label("Advance Count")]
        [Tooltip("The number of times that a line advance occurs before the current line is cancelled.")]
        public int advanceRequestsBeforeCancellingLine = 2;

        /// <summary>
        /// The number of times that this object has received an indication that
        /// the line should be advanced.
        /// </summary>
        /// <remarks>
        /// This value is reset to zero when a new line is run. When the line is
        /// advanced, this value is incremented. If this value ever meets or
        /// exceeds <see cref="advanceRequestsBeforeCancellingLine"/>, the line
        /// will be cancelled.
        /// </remarks>
        private int numberOfAdvancesThisLine = 0;

        /// <summary>
        /// The type of input that this line advancer responds to.
        /// </summary>
        public enum InputMode
        {
            /// <summary>
            /// The line advancer responds to Input Actions from the <a
            /// href="https://docs.unity3d.com/Packages/com.unity.inputsystem@latest">Unity
            /// Input System</a>.
            /// </summary>
            InputActions,
            /// <summary>
            /// The line advancer responds to keypresses on the keyboard.
            /// </summary>
            KeyCodes,
            /// <summary>
            /// The line advancer does not respond to any input.
            /// </summary>
            /// <remarks>When a line advancer's <see cref="inputMode"/> is set
            /// to <see cref="None"/>, call the <see
            /// cref="RequestLineHurryUp"/>, <see cref="RequestNextLine"/> and
            /// <see cref="RequestDialogueCancellation"/> methods directly from
            /// your code to control line advancement.</remarks>
            None,
            /// <summary>
            /// The line advancer responds to input from the legacy <a
            /// href="https://docs.unity3d.com/Manual/class-InputManager.html">Input
            /// Manager</a>.
            /// </summary>
            LegacyInputAxes,
        }

        /// <summary>
        /// The type of input that this line advancer responds to.
        /// </summary>
        /// <seealso cref="InputMode"/>
        [Tooltip("The type of input that this line advancer responds to.")]
        [Space]
        [MessageBox(sourceMethod: nameof(ValidateInputMode))]
        [SerializeField] InputMode inputMode;

        /// <summary>
        /// Validates the current value of <see cref="inputMode"/>, and
        /// potentially returns a message box to display.
        /// </summary>
        private MessageBoxAttribute.Message ValidateInputMode()
        {
#pragma warning disable CS0162 // Unreachable code detected

#if USE_INPUTSYSTEM
            const bool inputSystemInstalled = true;
#else
            const bool inputSystemInstalled = false;
#endif

#if ENABLE_INPUT_SYSTEM
            const bool enableInputSystem = true;
#else
            const bool enableInputSystem = false;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            const bool enableLegacyInput = true;
#else
            const bool enableLegacyInput = false;
#endif

            if (this.inputMode == InputMode.None)
            {
                return MessageBoxAttribute.Info($"To use this component, call the following methods on it:\n\n" +
                    $"- {nameof(this.RequestLineHurryUp)}()\n" +
                    $"- {nameof(this.RequestNextLine)}()\n" +
                    $"- {nameof(this.RequestDialogueCancellation)}()"
                );
            }

            if (this.inputMode == InputMode.LegacyInputAxes && !enableLegacyInput)
            {
                return MessageBoxAttribute.Warning("The Input Manager (Old) system is not enabled.\n\nEither change this setting to Input Actions, or enable Input Manager (Old) in Project Settings > Player > Configuration > Active Input Handling.");
            }

            if (this.inputMode == InputMode.InputActions)
            {
                if (inputSystemInstalled == false)
                {
                    return MessageBoxAttribute.Warning("Please install the Unity Input System package.");
                }
                if (!enableInputSystem)
                {
                    return MessageBoxAttribute.Warning("The Input System is not enabled.\n\nEither change this setting, or enable Input System in Project Settings > Player > Configuration > Active Input Handling.");
                }
            }

            return MessageBoxAttribute.NoMessage;
#pragma warning restore CS0162 // Unreachable code detected
        }

#if USE_INPUTSYSTEM
        /// <summary>
        /// The Input Action that triggers a request to advance to the next
        /// piece of content.
        /// </summary>
        [ShowIf(nameof(inputMode), InputMode.InputActions)]
        [Indent]
        [SerializeField] UnityEngine.InputSystem.InputActionReference? hurryUpLineAction;

        /// <summary>
        /// The Input Action that triggers an instruction to cancel the current
        /// line.
        /// </summary>
        [ShowIf(nameof(inputMode), InputMode.InputActions)]
        [Indent]
        [SerializeField] UnityEngine.InputSystem.InputActionReference? nextLineAction;

        /// <summary>
        /// The Input Action that triggers an instruction to cancel the entire
        /// dialogue.
        /// </summary>
        [ShowIf(nameof(inputMode), InputMode.InputActions)]
        [Indent]
        [SerializeField] UnityEngine.InputSystem.InputActionReference? cancelDialogueAction;

        /// <summary>
        /// If true, the <see cref="hurryUpLineAction"/>, <see
        /// cref="nextLineAction"/> and <see cref="cancelDialogueAction"/> Input
        /// Actions will be enabled when the the dialogue runner signals that a
        /// line is running.
        /// </summary>
        [Tooltip("If true, the input actions above will be enabled when a line begins.")]
        [ShowIf(nameof(inputMode), InputMode.InputActions)]
        [Indent]
        [SerializeField] bool enableActions = true;
#endif
        /// <summary>
        /// The legacy Input Axis that triggers a request to advance to the next
        /// piece of content.
        /// </summary>
        [ShowIf(nameof(inputMode), InputMode.LegacyInputAxes)]
        [Indent]
        [SerializeField] string? hurryUpLineAxis = "Jump";
        /// <summary>
        /// The legacy Input Axis that triggers an instruction to cancel the
        /// current line.
        /// </summary>
        [ShowIf(nameof(inputMode), InputMode.LegacyInputAxes)]
        [Indent]
        [SerializeField] string? nextLineAxis = "Cancel";
        /// <summary>
        /// The legacy Input Axis that triggers an instruction to cancel the
        /// entire dialogue.
        /// </summary>
        [ShowIf(nameof(inputMode), InputMode.LegacyInputAxes)]
        [Indent]
        [SerializeField] string? cancelDialogueAxis = "";


        /// <summary>
        /// The <see cref="KeyCode"/> that triggers a request to advance to the
        /// next piece of content.
        /// </summary>
        [ShowIf(nameof(inputMode), InputMode.KeyCodes)]
        [Indent]
        [SerializeField] KeyCode hurryUpLineKeyCode = KeyCode.Space;

        /// <summary>
        /// The <see cref="KeyCode"/> that triggers an instruction to cancel the
        /// current line.
        /// </summary>
        [ShowIf(nameof(inputMode), InputMode.KeyCodes)]
        [Indent]
        [SerializeField] KeyCode nextLineKeyCode = KeyCode.Escape;

        /// <summary>
        /// The <see cref="KeyCode"/> that triggers an instruction to cancel the
        /// entire dialogue.
        /// </summary>
        [ShowIf(nameof(inputMode), InputMode.KeyCodes)]
        [Indent]
        [SerializeField] KeyCode cancelDialogueKeyCode = KeyCode.None;

#if USE_INPUTSYSTEM
        private void OnHurryUpLinePerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            RequestLineHurryUp();
        }
        private void OnNextLinePerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            RequestNextLine();
        }
        private void OnCancelDialoguePerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            RequestDialogueCancellation();
        }
#endif

        /// <summary>
        /// Called by a dialogue runner when dialogue starts to add input action
        /// handlers for advancing the line.
        /// </summary>
        /// <returns>A completed task.</returns>
        public override YarnTask OnDialogueStartedAsync()
        {
#if USE_INPUTSYSTEM
            if (inputMode == InputMode.InputActions)
            {
                // If we're using the input system, register callbacks to run
                // when our actions are performed.
                if (hurryUpLineAction != null) { hurryUpLineAction.action.performed += OnHurryUpLinePerformed; }
                if (nextLineAction != null) { nextLineAction.action.performed += OnNextLinePerformed; }
                if (cancelDialogueAction != null) { cancelDialogueAction.action.performed += OnCancelDialoguePerformed; }
            }
#endif

            return YarnTask.CompletedTask;
        }

        /// <summary>
        /// Called by a dialogue runner when dialogue ends to remove the input
        /// action handlers.
        /// </summary>
        /// <returns>A completed task.</returns>
        public override YarnTask OnDialogueCompleteAsync()
        {
#if USE_INPUTSYSTEM
            // If we're using the input system, remove the callbacks.
            if (inputMode == InputMode.InputActions)
            {
                if (hurryUpLineAction != null) { hurryUpLineAction.action.performed -= OnHurryUpLinePerformed; }
                if (nextLineAction != null) { nextLineAction.action.performed -= OnNextLinePerformed; }
                if (cancelDialogueAction != null) { cancelDialogueAction.action.performed -= OnCancelDialoguePerformed; }
            }
#endif

            return YarnTask.CompletedTask;
        }

        /// <summary>
        /// Called by a dialogue view to signal that a line is running.
        /// </summary>
        /// <inheritdoc cref="AsyncLineView.RunLineAsync" path="/param"/>
        /// <returns>A completed task.</returns>
        public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            // A new line has come in, so reset the number of times we've seen a
            // request to skip.
            numberOfAdvancesThisLine = 0;

#if USE_INPUTSYSTEM
            if (enableActions)
            {
                if (hurryUpLineAction != null) { hurryUpLineAction.action.Enable(); }
                if (nextLineAction != null) { nextLineAction.action.Enable(); }
                if (cancelDialogueAction != null) { cancelDialogueAction.action.Enable(); }
            }
#endif

            return YarnTask.CompletedTask;
        }

        /// <summary>
        /// Called by a dialogue view to signal that options are running.
        /// </summary>
        /// <inheritdoc cref="AsyncLineView.RunOptionsAsync" path="/param"/>
        /// <returns>A completed task indicating that no option was selected by
        /// this view.</returns>
        public override YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, CancellationToken cancellationToken)
        {
            // This line view doesn't take any actions when options are
            // presented.
            return YarnTask<DialogueOption?>.FromResult(null);
        }

        /// <summary>
        /// Requests that the line be hurried up.
        /// </summary>
        /// <remarks>If this method has been called more times for a single line
        /// than <see cref="numberOfAdvancesThisLine"/>, this method requests
        /// that the dialogue runner proceed to the next line. Otherwise, it
        /// requests that the dialogue runner instruct all line views to hurry
        /// up their presentation of the current line.
        /// </remarks>
        public void RequestLineHurryUp()
        {
            // Increment our counter of line advancements, and depending on the
            // new count, request that the runner 'soft-cancel' the line or
            // cancel the entire line

            numberOfAdvancesThisLine += 1;

            if (multiAdvanceIsCancel && numberOfAdvancesThisLine >= advanceRequestsBeforeCancellingLine)
            {
                RequestNextLine();
            }
            else
            {
                if (runner != null)
                {
                    runner.RequestHurryUpLine();
                }
                else
                {
                    Debug.LogError($"{nameof(LineAdvancer)} dialogue runner is null", this);
                    return;
                }
            }
        }

        /// <summary>
        /// Requests that the dialogue runner proceeds to the next line.
        /// </summary>
        public void RequestNextLine()
        {
            if (runner != null)
            {
                runner.RequestNextLine();
            }
            else
            {
                Debug.LogError($"{nameof(LineAdvancer)} dialogue runner is null", this);
                return;
            }
        }

        /// <summary>
        /// Requests that the dialogue runner to instruct all line views to
        /// dismiss their content, and then stops the dialogue.
        /// </summary>
        public void RequestDialogueCancellation()
        {
            // Stop the dialogue runner, which will cancel the current line as
            // well as the entire dialogue.
            if (runner != null)
            {
                runner.Stop();
            }
        }

        /// <summary>
        /// Called by Unity every frame to check to see if, depending on <see
        /// cref="inputMode"/>, the <see cref="LineAdvancer"/> should take
        /// action.
        /// </summary>
        protected void Update()
        {
            switch (inputMode)
            {
                case InputMode.KeyCodes:
                    if (Input.GetKeyDown(hurryUpLineKeyCode)) { this.RequestLineHurryUp(); }
                    if (Input.GetKeyDown(nextLineKeyCode)) { this.RequestNextLine(); }
                    if (Input.GetKeyDown(cancelDialogueKeyCode)) { this.RequestDialogueCancellation(); }
                    break;
                case InputMode.LegacyInputAxes:
                    if (string.IsNullOrEmpty(hurryUpLineAxis) == false && Input.GetButtonDown(hurryUpLineAxis)) { this.RequestLineHurryUp(); }
                    if (string.IsNullOrEmpty(nextLineAxis) == false && Input.GetButtonDown(nextLineAxis)) { this.RequestNextLine(); }
                    if (string.IsNullOrEmpty(cancelDialogueAxis) == false && Input.GetButtonDown(cancelDialogueAxis)) { this.RequestDialogueCancellation(); }
                    break;
                default:
                    // Nothing to do; 'None' takes no action, and 'InputActions'
                    // doesn't poll in Update()
                    break;
            }
        }
    }

}
