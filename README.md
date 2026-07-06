## 湖界 LakeUI v3

![](https://img.shields.io/github/stars/Lake1059/LakeUI?label=星标) ![GitHub License](https://img.shields.io/github/license/Lake1059/LakeUI?label=许可证) ![](https://img.shields.io/github/downloads/Lake1059/LakeUI/total?label=Github%20下载量) ![](https://img.shields.io/nuget/dt/LakeUI?label=NuGet%20下载量)

LakeUI 是专为 WinForms 项目设计的一套精致交互控件，以我的昵称命名，官方中文名称 “湖界”。

LakeUI v1 采用 GDI+ 绘制所有图形、文字、动画和图片，为传统需求的 WinForms 项目提供了众多的精美控件；不过随着设计需求不断提升，GDI+ 已经无法满足性能要求，但是一刻也没有为 GDI+ 的性能而感到悲伤，因为已经赶到战场的是你所熟知的游戏图形接口。

LakeUI v2 使用 Direct2D、Direct3D、DirectWrite 加速绘制，由 [Vortice](https://github.com/amerkoleci/Vortice.Windows) 提供 DirectX 支持，现在大量的绘制都由 GPU 承担，配合大量新设计带来商业级控件的体验。曾经的遥不可及现在已成现实：窗体全透毛玻璃、超容器背景映射、高精度计时器驱动的动画，以及更多意想不到的惊喜。现在你的 WinForms 已经能够与所有主流 UI 框架甚至 Web 框架坐上同一张桌子打牌，这一切的代价只是升级运行库，除此之外没有任何代价，没有内存爆炸，没有显卡起火，更没有涨价！

LakeUI v3 打通了 GPU 的 “最后一公里”，现在最终呈现合成不再依赖 CPU，而是 GPU 直通 DWM 进行屏幕合成，与游戏引擎相当，渲染效率进一步提升，动画更丝滑。全新引入 HDR 支持，是的你没有看错，全球首个让 WinForms 用上 HDR 的控件库。这一切几乎没有代价，反而显存更低，综合消耗更少，当然还有最重要的是依旧没有涨价！

强烈建议通过 [NuGet](https://www.nuget.org/packages/LakeUI) 安装，包管理会自动安装所有依赖。如果你无法使用 NuGet，则需要自行想办法安装 Vortice.Direct2D1 和 Vortice.Direct3D11，要手动安装这些非常麻烦，其自身还有依赖。

NuGet：https://www.nuget.org/packages/LakeUI  
官网：https://lakeui.top  
购买许可证：[爱发电 ifdian.net](https://ifdian.net/item/15f0758814a911f1979752540025c377)  
Q群：1087964158

首发宣传视频：[BV1zeAHzEEKX](https://www.bilibili.com/video/BV1zeAHzEEKX)



<img src="LakeUI\LakeUI.png"/>

> [!CAUTION]
>
> LakeUI 是在新的 .NET 框架上开发的，以前的 Framework 和 Core 框架无法使用！<br>当然如果你知道怎么搞能用的话也可以 Fork 过去自己搞，我是懒得照顾了。<br>请注意查看位于此文件末尾的收费标准！<br>不会考虑制作 DataGridView！不要来问这个！

## 扩展包

[LakeUI.Notifications](LakeUI.Notifications/README.md)

## 核心优势

只要你使用新版本的 .NET 进行 WinForms 现代应用开发，从任何一个方面我都会强烈建议你来使用 LakeUI，这不是单纯的自荐，而是从所有角度上讲，LakeUI 的竞争力都已经所向披靡。

> 我们搞 WinForms 的开发者在选取一款 UI 组件的时候都会首选轻量级、简单易用、更新稳定的库，而不是沉重、复杂机制、更新摆烂的库。我们不想要去改变 WinForms 的核心优势，也就是这种像做 PPT 一样直接把东西摆上来然后定制效果直接所见即所得的超级优势，一个大型项目最重要的是可持续性可维护性。WinForms 的路不是只有工控，LakeUI 会打脸每一个觉得 WinForms 只能做生产环境项目的人，也会教训每一个觉得 WinForms 不值得已经被时代抛弃的人，你就说你用不用吧，那些喜欢拿跨平台来说事的人想必除了家财万贯的富人就是只能在互联网上口嗨的可怜人。

### 这个作者是真舍得

不要跟我提那些小卡拉米，只有最顶级的 AI 才配得上这个活<br>年费就是开，几百刀就是砸，花不起钱就别搞这项目<br>有需求直接提 issues，除了明确不做的，指不定哪天就顺手做了

### 全面高 DPI 支持

作为一个现代 UI，首先最重要的一点是高 DPI 支持，现在很多人已经用上了 4K 甚至 8K 显示器作为自己的工作平台，目前 150% 和 200% 的缩放倍率是主流选择，而市面上能给 WinForms 用的组件很少有支持良好的，这就会导致用户界面到处崩坏，除非开发者专门编写了支持逻辑，显然这要花费额外的工作量。LakeUI 在所有的逻辑上全部兼容了各种缩放，只要用户的分辨率足够，不管赛博灯泡还是定制的 1% 刻度都能够全自适应，甚至可以是在运行时即时调整的。

> 所以记住了，高 DPI 不友好是开发者的问题，菜就多练。旧框架除外。

### 平价位 DirectX GPU 加速

市面上几乎都在用 GDI+ 渲染，这是完全依靠 CPU 的，但 LakeUI 现在有更高的追求，市面上几乎只有大公司的商业级控件使用了 DX 路线，且不说有验证锁，那个价格也不是平民能承受的。而 LakeUI 从 2.0 开始将 DX 带到了普通开发者能够接受的平价位产品上，甚至没有因此涨价，更没有增加丝毫的使用难度，一切显卡的事情我全部处理好了。这个当然是用的三方对接库，我也尝试过自己写引擎，自己干一票还是太超模了。

### 激进的优化策略

性能一直是 LakeUI 高度关注的指标，LakeUI 不仅在全范围严格使用轻缓存机制，还在个别地方拿出了上世纪游戏级别的优化思路，不仅对性能消耗有要求，对内存和显存占用也有要求。如果一个控件在 DEMO 都卡，甚至在主流性能上连 60 帧都稳不住，那就直接定为毫无优化。我的要求是在主流性能下达成：有动画的东西 120 帧是基操，180 帧还没有吃满单核才叫优化达标，没动画的东西不准有任何能感知到的卡顿，要卡去后台卡，UI 线程优先响应。

### 全动画可设置时长和帧率

敢问市面上有谁做到了。LakeUI 不仅大量的控件有动画，而且所有动画允许自由设置帧率和时长，支持设置无限帧率。这可是在 WinForms 上，过来人都很难相信这东西还玩上实机渲染动画了。

### 超采样抗锯齿 SSAA

富哥这边请，为有实力的bro提供额外消耗显卡性能的方法。你可以不用但我就是强塞给你，反正我自己平时都不会用，但这就是卖点。除开极个别高压控件外，只要是有界面绘制的东西标配 SSAA。要说 3D 游戏用 SSAA 那是让显卡欲火焚身，现在是 2D 的你就说是不是效果拔群吧。

### 强迫症级别的开发体验

全属性自带中文说明；全属性自带默认值，可直接在属性窗口中右键选择重置；字体和颜色属性支持自动跟随容器属性值。不要小看这些个细节，市面上很多产品都做不好。

### 超容器背景映射

这是我想出来的路子，所以是 LakeUI 特别原创，这是一种把控件坐标映射到目标控件上来取得目标背景作为自身基础背景的机制，正所谓超容器，不仅可以跨越容器，还可以跨越窗体，只要能访问就可以映射，绝大多数控件都接入了这套机制，只需设置 BackgroundSource 属性来指定目标控件，即可轻松做出像 Web 那样的全透明背景应用。

### 矢量几何文字渲染

厌倦了 Windows 在 150% 及以下缩放时会将文字进行简化显示的特性吗，这是系统 TrueType hinting 字节码与 GASP 表在发力。现在可以设置 `GlobalTextQuality = TextQualityMode.Outline` 来全局启用矢量模式，此时所有文字都会以原始细节渲染，绕过系统机制而不再被简化，让用户不安装 MacType 也能够享受到近似的效果，你还可以亲自调整该模式下的渲染细节。

## 控件索引

此处仅列出简要内容用于索引，按照设计时间排序，详细介绍请运行 LakeUI.Demo.exe 来实际上手体验。

|        | 控件名称 / 类名                                        | 中文名称                   | 制作类型 | 性能负载 |
| ------ | ------------------------------------------------------ | -------------------------- | -------- | -------- |
| 1      | ModernButton                                           | 现代化按钮                 | 全新     | 低 🟢     |
| 2      | ModernTextBox                                          | 现代化文本框               | 全新     | 中 🟡     |
| 3      | ModernComboBox                                         | 现代化下拉框               | 全新     | 低 🟢     |
| 4      | BooleanSwitch                                          | 布尔开关                   | 全新     | 低 🟢     |
| 5      | QuantumSwitch                                          | 量子开关                   | 全新     | 低 🟢     |
| 6      | ExcellentTrackBar                                      | 极好的滑动条               | 全新     | 低 🟢     |
| 7      | ListViewDirectReDraw                                   | 列表视图原地重绘           | 原版     | 增益 ❤️   |
| 8      | ReDrawContextMenuStrip                                 | 重绘的上下文菜单           | 原版     | 增益 ❤️   |
| 9      | ModernContextMenu                                      | 现代化上下文菜单           | 全新     | 低 🟢     |
| **10** | **UltraDetailListView**                                | **极致的详细信息列表视图** | **全新** | **高 🔴** |
| 11     | ModernTabListControl                                   | 现代化竖向选项卡           | 全新     | 低 🟢     |
| 12     | ModernTabControl                                       | 现代化横向选项卡           | 全新     | 低 🟢     |
| 13     | ModernPanel                                            | 现代化容器                 | 原版     | 低 🟢     |
| 14     | ModernListBox                                          | 现代化列表框               | 全新     | 低 🟢     |
| 15     | HtmlColorLabel                                         | 支持 HTML 颜色标记的标签   | 全新     | 低 🟢     |
| 16     | ModernFontDialog                                       | 现代化字体选择对话框窗口   | 组合件   | 低 🟢     |
| 17     | ModernColorDialog                                      | 现代化颜色选择对话框窗口   | 组合件   | 低 🟢     |
| 18     | ExcellentProgressBar                                   | 极好的进度条               | 全新     | 低 🟢     |
| 19     | RoundDashBoard                                         | 圆形仪表盘                 | 全新     | 低 🟢     |
| 20     | JustEmptyControl                                       | (拿来填空白段的)           | N/A      | N/A      |
| 21     | ModernCheckBox                                         | 现代化复选/单选框          | 全新     | 低 🟢     |
| **22** | **ThisIsYourWindow**                                   | **窗口样式定制器 (D3D11)** | **全新** | **中 🟡** |
| 23     | MarkDownViewer                                         | MarkDown 简易查看器        | 全新     | 中 🟡     |
| 24     | ProgressRing                                           | 无进度的加载动画           | 全新     | 低 🟢     |
| 25     | SysTaskBarProgress                                     | 系统任务栏进度显示类       | N/A      | 低 🟢     |
| 26     | PixelPictureBox                                        | 像素级框选的图片框         | 全新     | 低 🟢     |
| 27     | TaskbarThumbnailToolbar                                | 任务栏缩略图工具栏         | N/A      | 低 🟢     |
| 28     | DwmWindowStyle                                         | DWM 窗口样式控制类         | N/A      | N/A      |
| 29     | ExMsgBox                                               | 重做的消息对话框           | 全新     | 低 🟢     |
| 30     | ExOverlayMsgBox                                        | 全屏或窗体遮罩对话框       | 全新     | 低 🟢     |
| 31     | ExFloatingBox                                          | 浮动的小型对话框           | 全新     | 低 🟢     |
| 32     | ExInputBox                                             | 重做的输入对话框           | 全新     | 低 🟢     |
| 33     | ExFloatingTip                                          | 浮动小提示                 | 全新     | 低 🟢     |
| 34     | CpuMonitor                                             | CPU 监控器                 | 全新     | 低 🟢     |
| 35     | RamMonitor                                             | RAM 监控器                 | 全新     | 低 🟢     |
| 36     | GpuMonitor                                             | GPU 监控静态类             | 全新     | 低 🟢     |
| 37     | BreadcrumbNavigationBar                                | 面包屑导航条               | 全新     | 低 🟢     |
| 38     | PrecisionTimer                                         | 高精度计时器               | N/A      | 低 🟢     |
| 39     | AgentRoom                                              | 智能体聊天室               | 全新     | 低 🟢     |
| 40     | ModernNumericUpDown                                    | 现代化数字框               | 全新     | 低 🟢     |
| 41     | MemberWall                                             | 成员墙（3FUI 定制）        | N/A      | N/A      |
| 42     | EasyStatesPanel                                        | 简易状态板（3FUI 定制）    | N/A      | N/A      |
| 43     | [LakeUI.Notifications](LakeUI.Notifications/README.md) | 封装的 Win10/11 系统通知   | N/A      | N/A      |
| 44     | Ultra2DChart                                           | 极致的二维图表             | 全新     | 中 🟡     |

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
