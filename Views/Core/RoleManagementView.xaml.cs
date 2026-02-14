using System.Windows.Controls;

namespace JoSystem.Views.Core
{
    public partial class RoleManagementView : UserControl
    {
        public RoleManagementView()
        {
            InitializeComponent();
            
            // 为 DataGrid 添加 LoadingRow 事件以显示正确序号
            var dataGrid = this.FindName("RoleDataGrid") as DataGrid;
            if (dataGrid != null)
            {
                dataGrid.LoadingRow += (s, e) => e.Row.Header = (e.Row.GetIndex() + 1).ToString();
            }
        }
    }
}
