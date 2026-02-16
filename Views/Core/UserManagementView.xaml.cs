using System.Windows.Controls;

namespace JoSystem.Views.Core
{
    public partial class UserManagementView : UserControl
    {
        public UserManagementView()
        {
            InitializeComponent();

            // 为 DataGrid 添加 LoadingRow 事件以显示正确序号
            if (this.FindName("UserDataGrid") is DataGrid dataGrid)
            {
                dataGrid.LoadingRow += (s, e) => e.Row.Header = (e.Row.GetIndex() + 1).ToString();
            }
        }
    }
}
