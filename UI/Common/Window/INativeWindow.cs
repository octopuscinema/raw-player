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

	public interface INativeWindow
	{
		void SetWindowTitle(string text);
		void ToggleFullscreen();
#nullable enable
		string? OpenFolderDialogue(string title, string defaultDirectory);
#nullable disable
		void EnableMenuItem(string name, bool enable);
		void CheckMenuItem(string name, bool check = true, bool uncheckSiblings = true);
		void Alert(AlertType alertType, string message, string title);
		void OpenUrl(string url);
		void Exit();
		Vector2i FramebufferSize { get; }
	}
}

