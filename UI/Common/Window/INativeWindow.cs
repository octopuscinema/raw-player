using System;
using System.Collections.Generic;
using System.Reflection;
using OpenTK.Mathematics;

namespace Octopus.Player.UI
{
	public enum AlertType
    {
		Blank,
		Information,
		Error,
		Warning,
		YesNo
    }

	public enum AlertResponse
	{
		None,
		Yes,
		No
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
        string? OpenFileDialogue(string title, string defaultDirectory, IReadOnlyCollection<Tuple<string, string>> extensionsDescriptions);
		string? SaveFileDialogue(string title, string defaultDirectory, IReadOnlyCollection<Tuple<string, string>> extensionsDescriptions);
#nullable disable
        void EnableMenuItem(string id, bool enable);
		void CheckMenuItem(string id, bool check = true, bool uncheckSiblings = true);
		bool MenuItemIsChecked(string id);
		void ToggleMenuItemChecked(string id);
		void SetMenuItemTitle(string id, string name);
		void AddMenuItem(string parentId, string name, uint? index, Action onClick, string? id = null);
		void RemoveMenuItem(string parentId, string id);
		bool MenuItemExists(string id);
        void AddMenuSeperator(string parentId, uint? index);
        void SetLabelContent(string id, string content, Vector3? colour = null, bool? fixedWidthDigitHint = null);
		void SetButtonVisibility(string id, bool visible);
		void SetButtonEnabled(string id, bool enabled);
		void SetSliderValue(string id, float value);
		void SetSliderEnabled(string id, bool enabled);
        AlertResponse Alert(AlertType alertType, string message, string title);
		Core.Error SavePng(string path, byte[] data, in Vector2i dimensions, GPU.Format format, bool ignoreAlpha = true);
        void OpenContextMenu(string id);
#nullable enable
        void OpenContextMenu(List<string> mainMenuItems, List<(string, string)?>? additionalItems = null);
#nullable disable
        void OpenAboutPanel();
        void OpenUrl(string url);
		void OpenTextEditor(string textFilePath);
		void AnimateInControls();
		void AnimateOutControls();
		void Notification(string title, string caption);
        ControlsAnimationState ControlsAnimationState { get; }
		void Exit();
		Vector2i FramebufferSize { get; }
		bool MouseInsidePlaybackControls { get; }
		void InvokeOnUIThread(Action action, bool async = true);
		PlayerApplication PlayerApplication { get; }
		bool DropAreaVisible { get; set; }
		void ShowInNavigator(List<string> paths);
        bool RenderContinuouslyHint { set; }
    }
}

