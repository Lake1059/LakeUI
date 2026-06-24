Imports System.ComponentModel

''' <summary>
''' Markdown 渲染控件兼容包装。实际解析、布局、绘制和图片缓存逻辑由通用 MarkdownViewerCore 承载。
''' </summary>
<DefaultEvent("LinkClicked")>
Public Class MarkDownViewer

    Public Sub New()
        InitializeComponent()
    End Sub

End Class
