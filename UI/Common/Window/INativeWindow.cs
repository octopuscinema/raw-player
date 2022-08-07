using System;
using System.Reflection;
using OpenTK.Mathematics;

namespace Octopus.Player.UI
{
	public enum AlertType
    {
		Blank,
		Information,
		Error,
		Warning
    }

	public enum ControlsAnimationState
    {
		In,
		Out
    }

	public interface INativeWindow
	{
		void LockAspect(Core.Maths.Rational ratio);
		void UnlockAspect();
		bool AspectLocked { get; }
		void SetWindowTitle(string text);
		void ToggleFullscreen();
#nullable enable
		string? OpenFolderDialogue(string title, string defaultDirectory);
#nullable disable
		void EnableMenuItem(string id, bool enable);
		void CheckMenuItem(string id, bool check = true, bool uncheckSiblings = true);
		bool MenuItemIsChecked(string id);
		void ToggleMenuItemChecked(string id);
		void SetMenuItemTitle(string id, string name);
		void SetLabelContent(string id, string content, Vector3? colour = null);
		void SetButtonVisibility(string id, bool visible);
		void SetButtonEnabled(string id, bool enabled);
		void SetSliderValue(string id, float value);
		void SetSliderEnabled(string id, bool enabled);
		void Alert(AlertType alertType, string message, string title);
		void OpenUrl(string url);
		void AnimateInControls();
		void AnimateOutControls();
		ControlsAnimationState ControlsAnimationState { get; }
		void Exit();
		Vector2i FramebufferSize { get; }
		bool MouseInsidePlaybackControls { get; }
		void InvokeOnUIThread(Action action, bool async = true);
	}
}

