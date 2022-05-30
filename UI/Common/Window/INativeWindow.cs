namespace Octopus.Player.UI
{
	public interface INativeWindow
	{
		void SetWindowTitle(string text);
		void ToggleFullscreen();
#nullable enable
		string? OpenFolderDialogue(string title, string defaultDirectory);
#nullable disable
		void InformationAlert(string message, string title);
		void Exit();
	}
}

