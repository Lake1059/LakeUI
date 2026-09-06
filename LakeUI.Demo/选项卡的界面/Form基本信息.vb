
Public Class Form基本信息
    Private Sub Form基本信息_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.MarkDownViewer1.Text = $"## 湖界 LakeUI v5

![](https://img.shields.io/github/stars/Lake1059/LakeUI.png?label=星标) ![GitHub License](https://img.shields.io/github/license/Lake1059/LakeUI.png?label=许可证) ![](https://img.shields.io/github/downloads/Lake1059/LakeUI/total.png?label=Github%20下载量) ![](https://img.shields.io/nuget/dt/LakeUI.png?label=NuGet%20下载量)

LakeUI（官方中文名称：湖界）是一套面向现代 .NET WinForms 的交互控件库。它保留了 WinForms 拖放即用、所见即所得的开发方式，同时把高 DPI、动画、透明背景、DirectWrite 文字和 GPU 加速渲染带进传统桌面应用，让开发者不必更换技术栈，也能构建细腻、流畅且高度可定制的界面。

LakeUI 从 v1 的 GDI+ 全量绘制起步，在 v2 引入由 [Vortice.Windows](https://github.com/amerkoleci/Vortice.Windows) 提供支持的 Direct2D、Direct3D 与 DirectWrite，并在 v3 打通 GPU 到 DWM 的最终呈现链路、加入 HDR 输出。一路演进的目标始终没变：在不破坏 WinForms 原生开发体验的前提下，把更多图形工作交给 GPU，以更低的综合开销换来更稳定的高帧率动画和更丰富的视觉效果。

LakeUI v5 使用全新的每控件 GPU 渲染架构，控件拥有独立的 HWND Swap Chain，通过 GPU 绘制并直接提交至 DWM，WinForms 的 `OnPaint` 只负责触发首次或恢复呈现，不再经过 HDC/Graphics 兼容桥，在降低重复资源占用的同时，让控件刷新彼此隔离、动画响应更加稳定。

V5 渲染引擎的代价：V5 没有 CPU 纹理层，原版控件无法再通过设置背景透明的方式来兼容背景映射，并且还会导致自照乱渲染的问题，所以如果要和原版控件一起用的话原版控件不能设置为透明；其次设计器中的焦点和手柄是走 CPU 渲染的，V5 控件不会渲染这个，取而代之的是 LakeUI 自行绘制的焦点框和手柄，当然手柄的实际功能是设计器原生提供的，所以手柄的绘制位置会与实际响应的位置有些差异，毕竟控件自绘没法画到外边去，这个知道一下就行了而且也修不了，反正不怎么影响设计器体验。

NuGet：https://www.nuget.org/packages/LakeUI  
官网：https://lakeui.top  
购买许可证：[爱发电 ifdian.net](https://ifdian.net/item/15f0758814a911f1979752540025c377)  
Q群：1087964158"
    End Sub

    Private Sub MarkDownViewer1_LinkClicked(sender As Object, e As LinkClickedEventArgs) Handles MarkDownViewer1.LinkClicked
        Process.Start(New ProcessStartInfo With {.FileName = e.LinkText, .UseShellExecute = True})
    End Sub
End Class
