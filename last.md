# NatureCalls 房间号与局域网发现报告

## 改动

- 新增 `scripts/room_broker.py`：TCP 8100 HTTP/JSON broker，随机三位房间号、8001–8099 端口池、12 房间上限、单 IP 5 次/分钟、状态持久化/恢复、systemd 健康检查、停止清理和协议版本。
- 新增 `scripts/naturecalls-broker.service` 与 `scripts/naturecalls-room@.service`。动态房间使用 `-idle-exit 300`，8000 常驻模板保持原路径。
- `Session` 支持默认 broker `49.233.132.244:8100`、`-broker host:port`、创建/查询三位房间和 `-idle-exit` 空闲退出；大厅保留 Relay、IP:端口直连。
- `LanBeacon` 为 Core 纯逻辑；`LanDiscovery` 提供 UDP 47777 信标收发和 4 秒过期。Lobby UI 增加服务器房间按钮、LAN 房间列表和「房间号 / IP:端口」占位文本；通过 `LobbyFlowBuilder` 重新生成 `LobbyCanvas.prefab`。
- 更新部署脚本、README、PLAYGROUND、ARCHITECTURE、DEVELOPMENT。未修改 `GLOSSARY.md`。

## 设计偏差

- 动态模板采用新名称 `naturecalls-room@.service`，broker 与部署脚本均已同步；动态房间部署时不强制重启。
- LAN 列表与信标已接入运行时，但本机自动发现和两客户端房间号端到端流程未在本轮执行。
- broker 的 systemd readiness 依赖服务器上的 `ss` 与 `journalctl` 输出；本机单测用假 systemctl/探针覆盖。

## 验证

- 通过：`python3 -m unittest discover -s scripts/tests -v`（39 项）。
- 通过：`python3 scripts/check.py --require-generated`（1491 文件、755 GUID）。
- 通过：Unity EditMode，Artifacts/20261125T115309Z-09620c-test-editmode（41/41）。
- 通过：Unity PlayMode，Artifacts/20261125T120014Z-e43fb5-test-playmode（42 通过、1 个既有 Ignored）。
- 通过：macOS 构建，`Builds/macOS/20261125T112914Z-eb55fe/NatureCalls.app`。
- 通过：Linux Server 构建，`Builds/LinuxServer/20261125T113652Z-ee569f/NatureCalls`，桌面 Player 恢复通过。
- 通过：`python3 scripts/unity.py lobby-flow`，生成后的 Lobby prefab 已包含新增按钮和 LAN 列表引用。

## 未验证与风险

- 未登录、未 SSH、未部署课程服务器；未改防火墙或其他系统配置。
- 未执行用户要求的本机双客户端 broker + `-idle-exit 20` 端到端探针；需要可运行的 macOS 客户端和假的 systemctl 启动器。
- 未在 macOS 回环/真实局域网验证 UDP 广播；部分网络环境可能限制广播或端口 47777。
- `Artifacts/` 与 `Builds/` 均未纳入提交；Unity 自动修改的 `ProjectSettings/ProjectSettings.asset` 已恢复。
