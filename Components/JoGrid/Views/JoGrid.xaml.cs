using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows;

namespace JoSystem.Components.JoGrid
{
    [ContentProperty(nameof(Columns))]
    public partial class JoGrid : UserControl
    {
        public JoGrid()
        {
            InitializeComponent();
            Loaded += (_, __) => SyncColumns();
            Columns.CollectionChanged += (_, __) => SyncColumns();
            PART_DataGrid.LoadingRow += (s, e) => e.Row.Header = (e.Row.GetIndex() + 1).ToString();
            PART_Prev.Click += (_, __) => GoPrev();
            PART_Next.Click += (_, __) => GoNext();
            UpdateGridItems();
        }

        public object ItemsSource
        {
            get => GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(object), typeof(JoGrid), new PropertyMetadata(null, OnItemsSourceChanged));

        public object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(JoGrid));

        public bool AutoGenerateColumns
        {
            get => (bool)GetValue(AutoGenerateColumnsProperty);
            set => SetValue(AutoGenerateColumnsProperty, value);
        }
        public static readonly DependencyProperty AutoGenerateColumnsProperty =
            DependencyProperty.Register(nameof(AutoGenerateColumns), typeof(bool), typeof(JoGrid), new PropertyMetadata(false));

        public ObservableCollection<DataGridColumn> Columns { get; } = new ObservableCollection<DataGridColumn>();

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }
        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(JoGrid), new PropertyMetadata(false));

        public DataGridSelectionMode SelectionMode
        {
            get => (DataGridSelectionMode)GetValue(SelectionModeProperty);
            set => SetValue(SelectionModeProperty, value);
        }
        public static readonly DependencyProperty SelectionModeProperty =
            DependencyProperty.Register(nameof(SelectionMode), typeof(DataGridSelectionMode), typeof(JoGrid), new PropertyMetadata(DataGridSelectionMode.Single));

        public double RowHeight
        {
            get => (double)GetValue(RowHeightProperty);
            set => SetValue(RowHeightProperty, value);
        }
        public static readonly DependencyProperty RowHeightProperty =
            DependencyProperty.Register(nameof(RowHeight), typeof(double), typeof(JoGrid), new PropertyMetadata(40.0));

        public bool EnableSorting
        {
            get => (bool)GetValue(EnableSortingProperty);
            set => SetValue(EnableSortingProperty, value);
        }
        public static readonly DependencyProperty EnableSortingProperty =
            DependencyProperty.Register(nameof(EnableSorting), typeof(bool), typeof(JoGrid), new PropertyMetadata(true));

        public bool EnablePaging
        {
            get => (bool)GetValue(EnablePagingProperty);
            set => SetValue(EnablePagingProperty, value);
        }
        public static readonly DependencyProperty EnablePagingProperty =
            DependencyProperty.Register(nameof(EnablePaging), typeof(bool), typeof(JoGrid), new PropertyMetadata(false, OnPagingChanged));

        public int PageSize
        {
            get => (int)GetValue(PageSizeProperty);
            set => SetValue(PageSizeProperty, value);
        }
        public static readonly DependencyProperty PageSizeProperty =
            DependencyProperty.Register(nameof(PageSize), typeof(int), typeof(JoGrid), new PropertyMetadata(20, OnPagingChanged));

        public DataTemplate ActionTemplate
        {
            get => (DataTemplate)GetValue(ActionTemplateProperty);
            set => SetValue(ActionTemplateProperty, value);
        }
        public static readonly DependencyProperty ActionTemplateProperty =
            DependencyProperty.Register(nameof(ActionTemplate), typeof(DataTemplate), typeof(JoGrid), new PropertyMetadata(null, OnActionTemplateChanged));

        private IList _originalItems;
        private int _pageIndex;

        private void SyncColumns()
        {
            if (PART_DataGrid == null) return;
            PART_DataGrid.Columns.Clear();
            foreach (var col in Columns)
            {
                PART_DataGrid.Columns.Add(col);
            }
            if (ActionTemplate != null)
            {
                var actionCol = new DataGridTemplateColumn
                {
                    Header = new System.Windows.Controls.TextBlock { Text = (string)Application.Current.TryFindResource("Lang.Settings.Action") ?? "操作" },
                    CellTemplate = ActionTemplate,
                    Width = 100
                };
                PART_DataGrid.Columns.Add(actionCol);
            }
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = (JoGrid)d;
            grid._originalItems = (e.NewValue as IEnumerable)?.Cast<object>().ToList();
            grid._pageIndex = 0;
            grid.UpdateGridItems();
        }

        private static void OnPagingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = (JoGrid)d;
            grid._pageIndex = 0;
            grid.UpdateGridItems();
        }

        private static void OnActionTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = (JoGrid)d;
            grid.SyncColumns();
        }

        private void UpdateGridItems()
        {
            if (PART_DataGrid == null) return;
            if (!EnablePaging || _originalItems == null)
            {
                PART_DataGrid.ItemsSource = ItemsSource as IEnumerable;
                PART_PageInfo.Text = string.Empty;
                return;
            }
            var total = _originalItems.Count;
            var pages = Math.Max(1, (int)Math.Ceiling(total / (double)Math.Max(1, PageSize)));
            _pageIndex = Math.Max(0, Math.Min(_pageIndex, pages - 1));
            var pageItems = new System.Collections.Generic.List<object>();
            var start = _pageIndex * PageSize;
            var end = Math.Min(total, start + Math.Max(1, PageSize));
            for (var i = start; i < end; i++)
            {
                pageItems.Add(_originalItems[i]);
            }
            PART_DataGrid.ItemsSource = pageItems;
            PART_PageInfo.Text = $"第 {_pageIndex + 1} / {pages} 页，共 {total} 条";
            PART_Prev.IsEnabled = _pageIndex > 0;
            PART_Next.IsEnabled = _pageIndex < pages - 1;
        }

        private void GoPrev()
        {
            if (_pageIndex <= 0) return;
            _pageIndex--;
            UpdateGridItems();
        }
        private void GoNext()
        {
            _pageIndex++;
            UpdateGridItems();
        }
    }
}
