

Public Class Form许可证
    Private Sub Form许可证_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.MarkDownViewer1.Text = $"## 收费标准

请注意，LakeUI 是收费软件，要在公开发布的产品中使用必须取得对应许可，以下列出了不同用途的收费标准。LakeUI 主要面向个人项目提供，可以直接在 [爱发电 ifdian.net](https://ifdian.net/item/15f0758814a911f1979752540025c377) 和 Payhip (暂时懒得上架) 上购买，无需通过其他方式联系我。此处列出的价格仅供参考，因为可能受到汇率影响，请以销售平台上的实际价格为准。

> [!CAUTION]
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

LakeUI 使用 GPL-3.0-only 开源协议，如果正在使用免费的自由许可证，请遵守该协议的条款；如果使用赞助许可证、商业许可证、企业定制订阅，则可以不遵守该协议，无需询问我。"
    End Sub

    Private Sub MarkDownViewer1_LinkClicked(sender As Object, e As LinkClickedEventArgs) Handles MarkDownViewer1.LinkClicked
        Process.Start(New ProcessStartInfo With {.FileName = e.LinkText, .UseShellExecute = True})
    End Sub
End Class