
Public Class Form基本信息
    Private Sub Form基本信息_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.MarkDownViewer1.Text = $"## 湖界 LakeUI v2

![](https://img.shields.io/github/stars/Lake1059/LakeUI.png?label=星标) ![GitHub License](https://img.shields.io/github/license/Lake1059/LakeUI.png?label=许可证) ![](https://img.shields.io/github/downloads/Lake1059/LakeUI/total.png?label=Github%20下载量) ![](https://img.shields.io/nuget/dt/LakeUI.png?label=NuGet%20下载量)

LakeUI 是专为 WinForms 项目设计的一套精致交互控件，以我的昵称命名，官方中文名称：湖界。

LakeUI v1 采用 GDI+ 绘制所有图形、文字、动画和图片，为传统需求的 WinForms 项目提供了众多的精美控件；不过随着设计需求不断提升，GDI+ 已经无法满足性能要求，但是一刻也没有为 GDI+ 的性能而感到悲伤，因为已经赶到战场的是你所熟知的游戏图形接口。

LakeUI v2 使用 Direct2D、Direct3D、DirectWrite 加速绘制，由 [Vortice](https://github.com/amerkoleci/Vortice.Windows) 提供 DirectX 支持，现在大量的绘制都由 GPU 承担，配合大量新设计带来商业级控件的体验。曾经的遥不可及现在已成现实：窗体全透毛玻璃、超容器背景映射、高精度计时器驱动的动画，以及更多意想不到的惊喜。现在你的 WinForms 已经能够与所有主流 UI 框架甚至 Web 框架坐上同一张桌子打牌，这一切的代价只是升级运行库，除此之外没有任何代价，没有内存爆炸，没有显卡起火，更没有涨价！

强烈建议通过 [NuGet](https://www.nuget.org/packages/LakeUI) 安装，包管理会自动安装所有依赖。如果你无法使用 NuGet，则需要自行想办法安装 Vortice.Direct2D1 和 Vortice.Direct3D11，要手动安装这些非常麻烦，其自身还有依赖。

NuGet：https://www.nuget.org/packages/LakeUI  
官网：https://lakeui.top  
购买许可证：[爱发电 ifdian.net](https://ifdian.net/item/15f0758814a911f1979752540025c377)  
Q群：1087964158"
    End Sub

    Private Sub MarkDownViewer1_LinkClicked(sender As Object, e As LinkClickedEventArgs) Handles MarkDownViewer1.LinkClicked
        Process.Start(New ProcessStartInfo With {.FileName = e.LinkText, .UseShellExecute = True})
    End Sub
End Class