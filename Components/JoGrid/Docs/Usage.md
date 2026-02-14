# JoGrid 使用文档

JoGrid 是一个可复用的 WPF DataGrid 组件，提供统一样式与交互，同时支持高度自定义。将 Components/JoGrid 整个文件夹拷贝到其他项目即可使用。

## 引用

- XAML 引用命名空间：`xmlns:jo="clr-namespace:JoSystem.Components.JoGrid"`
- 使用示例：

```xml
<jo:JoGrid ItemsSource="{Binding Items}"
           SelectedItem="{Binding Current}"
           RowHeight="40"
           IsReadOnly="False"
           SelectionMode="Single"
           EnableSorting="True"
           EnablePaging="True"
           PageSize="20">
    <!-- 列定义 -->
    <DataGridTextColumn Header="#" Width="40"
                        Binding="{Binding RelativeSource={RelativeSource AncestorType=DataGridRow}, Path=Header}" IsReadOnly="True"/>
    <DataGridTextColumn Header="名称" Binding="{Binding Name}" Width="*" />

    <!-- 操作列模板（可选） -->
    <jo:JoGrid.ActionTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <Button Command="{Binding DataContext.EditCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                        CommandParameter="{Binding}" Style="{StaticResource SecondaryButtonStyle}" Content="编辑" Padding="8,2" Height="24"/>
                <Button Command="{Binding DataContext.DeleteCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                        CommandParameter="{Binding}" Style="{StaticResource DangerButtonStyle}" Content="删除" Padding="8,2" Height="24" Margin="8,0,0,0"/>
            </StackPanel>
        </DataTemplate>
    </jo:JoGrid.ActionTemplate>
</jo:JoGrid>
```

## 属性

- ItemsSource：数据源。支持任何 IEnumerable。
- SelectedItem：选中项双向绑定。
- AutoGenerateColumns：是否自动生成列（默认 False）。
- IsReadOnly：只读模式（默认 False）。
- SelectionMode：选择模式（Single/Extended）。
- RowHeight：行高（默认 40，可自定义）。
- EnableSorting：启用列排序（默认 True，需要列设置 SortMemberPath 或绑定属性）。
- EnablePaging：启用分页（默认 False）。
- PageSize：每页数量（默认 20，EnablePaging=True 时生效）。
- ActionTemplate：操作列模板（可选）。设置后会在最后追加一列。

## 交互与性能

- 开启虚拟化：行/列虚拟化与 Recycling 模式默认启用，提高性能。
- 分页：内置上一页/下一页与页信息，适合中大型列表；当数据量较小时可禁用分页。
- 排序：DataGrid 原生排序，建议为文本列设置 SortMemberPath 或确保绑定属性可排序。

## 风格

- JoGrid 不覆盖项目全局 DataGrid 样式，完全沿用 Assets/Styles.xaml。
- 若需要统一列宽与对齐，可在项目的 Styles.xaml 中定义针对 DataGridCell/DataGridColumnHeader 的样式，JoGrid会自然继承。

## 注意

- 使用 ActionTemplate 时，内部通过 DataGrid 相对绑定拿到 ViewModel 命令；你也可以替换为事件或其他交互。
- 启用分页后，DataGrid.ItemsSource 为当前页的集合；SelectedItem 仍按该集合项进行绑定。

