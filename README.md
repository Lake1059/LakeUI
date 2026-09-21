# 湖界 LakeUI v5 高刷时代 WinForms 控件库

![](https://img.shields.io/github/stars/Lake1059/LakeUI?label=星标) ![GitHub License](https://img.shields.io/github/license/Lake1059/LakeUI?label=许可证) ![](https://img.shields.io/github/downloads/Lake1059/LakeUI/total?label=Github%20下载量) ![](https://img.shields.io/nuget/dt/LakeUI?label=NuGet%20下载量)

> 仅支持 .NET 8+，请注意核对项目框架

> 让 WinForm 再次伟大！

LakeUI 全球首创在 WinForms 上的全 GPU 链路渲染管线，让这个传统的框架迈入 WinUI + GPU 时代，基于  [Vortice.Windows](https://github.com/amerkoleci/Vortice.Windows)，使用 Direct2D1、Direct3D11、DirectWrite 呈现。

LakeUI 并非为了还原 WinUI 风格，而是针对 WinForms 的实际需求和自由度从头设计的一套交互视觉，充分发挥窗体设计器的便捷优势，减少空间浪费和提高生产效率。还同时引入了一系列在 WinForms 历史上想都不敢想的设计（见下方无序列表）。同时由于引擎实现与游戏引擎相似，在全量重载和全屏缩放的时候容易触发显卡驱动的游戏识别，比如 NVIDIA APP 的信息浮窗提示。

- 超容器背景映射 + 窗口级背景
- 高 DPI 全适配缩放
- 高刷动画 + 打断动画
- 即时毛玻璃、磨砂玻璃
- 矢量几何完整细节文字渲染
- SSAA 超采样抗锯齿
- 真实 HDR 映射输出

LakeUI 的控件数量并不多，目前不到 50 个，但设计方向不在用数量取胜，而是每个控件的多用途。通过更多和更强的功能来减少实际开发中的控件数量以提升维护性，Visual Studio 的窗体设计器在过去的几十年中的性能改进并不大，甚至还在新 .NET 时代开倒车，因此减少每个页面的控件数量是十分有效的优化办法。以下是最值得一试的控件，极高的封装程度能节约大量控件浪费。

- 超多可定制内容的窗口样式定制器
- 全世界 WinForms 上最好的详细信息模式的列表视图
- 支持后台线程的高精计时器，可精确至 1ms
- 带子文本功能和涟漪特效的按钮
- 支持语法高亮的全自绘文本框
- 支持基础 HTML 语法和工具提示的标签
- 以及更多全新制作的强化原版控件

**限制与取舍：**LakeUI v5 没有 CPU 纹理层，原版控件无法再通过设置背景透明的方式来兼容背景映射，并且还会导致自照乱渲染的问题，所以如果要和原版控件一起用的话原版控件不能设置为透明；其次设计器中的焦点和手柄是走 CPU 渲染的，v5 控件不会渲染这个，取而代之的是 LakeUI 自行绘制的焦点框和手柄，当然手柄的实际功能是设计器原生提供的，所以手柄的绘制位置会与实际响应的位置有些差异，毕竟控件自绘没法画到外边去，这个知道一下就行了而且也修不了，反正不怎么影响设计器体验。

NuGet：https://www.nuget.org/packages/LakeUI <br>
官网：https://lakeui.top <br>
购买许可证：[爱发电 ifdian.net](https://ifdian.net/item/15f0758814a911f1979752540025c377) <br>
Q群：1087964158

<img src="LakeUI\LakeUI.png"/>

> [!CAUTION]
>
> LakeUI 是在新的 .NET 框架上开发的，以前的 Framework 和 Core 框架无法使用！<br>当然如果你知道怎么搞能用的话也可以 Fork 过去自己搞，我是懒得照顾了。<br>请注意查看位于此文件末尾的收费标准！<br>不会考虑制作 DataGridView！不要来问这个！

## 扩展包

[LakeUI.Notifications](LakeUI.Notifications/README.md)

## 最值得一试

------

想要一个更易于使用的字体选择对话框吗？<br>还是想要一个更加符合现代需求的颜色选择对话框？<br>只需要像以前一样 New 出对象并选择你的 Show

<img src="Image\ModernFontDialog.png" />

<img src="Image\ModernColorDialog.png" />

------

羡慕其他人用 Web 框架定制窗口样式？现在轮到他们羡慕你了<br>全尺寸、全颜色、对齐方位、可选分层阴影，全部可定制！<br>WinForms 现在也可以拥有比肩 Chrome、Edge、VS Code、VS，甚至 macOS 的窗口样式

<img src="Image\ThisIsYourWindow.png" />

------

轻量级 markdown 渲染控件，支持 AI 输出场景的增量渲染功能<br>支持选中复制内容、本地/在线图片、大部分基本样式等，还可定制元素效果

<img src="Image\MarkDownViewer.png" />

------

文本框现在支持行数显示和定制代码高亮模式，可满足轻量编程场景<br>虽无法完全复刻 Visual Studio 的特性，但已足够绝大多数需求<br>文本框自身仅提供高亮接口，实现方法可以查看 Demo 演示中这一部分的源码

<img src="Image\ModernTextBox_Code.png" />

------

专为像素级框选而生的图片框，自带缩放和框选功能<br>其自身并不提供放大镜视图位置，只是提供获取方法来直接返回图片成品<br>开发者仅需提供额外的图片框并调用方法即可实现四个角落的放大镜视图

<img src="Image\PixelPictureBox.png" />

## 收费标准

请注意，LakeUI 是收费软件，要在公开发布的产品中使用必须取得对应许可，以下列出了不同用途的收费标准。LakeUI 主要面向个人项目提供，可以直接在 [爱发电 ifdian.net](https://ifdian.net/item/15f0758814a911f1979752540025c377) 和 Payhip (暂时懒得上架) 上购买，无需通过其他方式联系我。此处列出的价格仅供参考，因为可能受到汇率影响，请以销售平台上的实际价格为准。

> [!CAUTION]
>
> 由于虚拟商品的特殊性和全自动发货机制<br>付款成功即收到唯一许可证编号，概不退换！<br>获得编号后您可以将其悬挂于您产品的关于板块以供社会监督。

### 自由许可证
如果您正在完全没有盈利的开源项目中使用，无需购买，直接使用即可。
+ 完全没有盈利的开源项目
+ 不能使用收款码、开通赞助、第三方广告
+ 必须完全开源
+ 在学校使用，用于完成学业或教学用途
+ 需要遵守 GPL-3.0 的其他条款

### 赞助许可证
顾名思义，如果您的项目只通过用户自愿赞助或第三方广告来盈利，选择此许可就对了，价格大约是 20 CNY。
+ 可以使用收款码、开通赞助、第三方广告
+ 可以是闭源或半开源
+ 不能有任何付费解锁的功能

### 商业许可证
显然，您需要项目的收益，不论您的项目是可选付费、强制付费、订阅制、买断制还是其他需要用户付费才能解锁的功能，选择此许可就对了，价格大约是 600 CNY。
+ 任何付费解锁的功能或服务
+ 任何付费模式

### 企业许可证
企业向来是不会使用 WinForms 的，所以不直接提供适用于企业的许可，当然如果确实需要，请联系我以定制订阅价格。

## 开源许可

LakeUI 使用 GPL-3.0-only 开源协议，如果正在使用免费的自由许可证，请遵守该协议的条款；如果使用赞助许可证、商业许可证、企业定制订阅，则可以不遵守该协议，无需询问我。
