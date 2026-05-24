using System.Windows.Forms;

namespace RRStoSQL.Helpers
{
	public static class MessageHelper
	{
		/// <summary>
		/// Окно с копируемым текстом
		/// </summary>
		public static void Show(
			string text,
			string title = "Информация")
		{
			Clipboard.SetText(
				text);

			Form form =
				new Form();

			form.Text =
				title;

			form.Width = 700;
			form.Height = 400;

			TextBox textBox =
				new TextBox();

			textBox.Multiline = true;
			textBox.ReadOnly = true;
			textBox.ScrollBars =
				ScrollBars.Both;

			textBox.Dock =
				DockStyle.Fill;

			textBox.Text =
				text;

			form.Controls.Add(
				textBox);

			form.ShowDialog();
		}
	}
}