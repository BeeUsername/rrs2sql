using RRStoSQL.Models;
using System.Windows.Forms;

namespace RRStoSQL.Forms.UI
{
	public class StepStatusManager
	{
		private readonly ListView _listView;

		public StepStatusManager(ListView listView)
		{
			_listView = listView;
		}

		public void SetStatus(
			string stepName,
			StepStatus status)
		{
			for (int i = 0;
				 i < _listView.Items.Count;
				 i++)
			{
				var item = _listView.Items[i];

				if (item.SubItems[1].Text == stepName)
				{
					SetStatus(i, status);

					return;
				}
			}
		}

		private void SetStatus(
			int index,
			StepStatus status)
		{
			if (index < 0 ||
				index >= _listView.Items.Count)
			{
				return;
			}

			string icon = "⌛";

			switch (status)
			{
				case StepStatus.Waiting:
					//icon = "⌛";
					icon = "...";
					break;

				case StepStatus.Running:
					icon = "⏳";
					break;

				case StepStatus.Success:
					icon = "✓";
					break;

				case StepStatus.Error:
					icon = "❌";
					break;
			}

			_listView.Items[index].Text = icon;

			_listView.Items[index]
				.EnsureVisible();
		}
	}
}