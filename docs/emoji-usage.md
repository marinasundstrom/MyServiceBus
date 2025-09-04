# Emoji Usage

Emojis highlight events and results throughout MyServiceBus documentation and samples. They offer quick visual cues and are not limited to logging—you might see them in comments or snippets to show what happened.

| Emoji | Context and meaning | Example |
|-------|--------------------|---------|
| 🚀 | Service or process starting. Shows when a host or component comes online. | `logger.LogInformation("🚀 Service bus started");` |
| 🛑 | Service or process stopping. Indicates a clean shutdown. | `logger.LogInformation("🛑 Service bus stopped");` |
| 📤 | Sending or publishing a message. Marks outbound traffic. | `logger.LogInformation("📤 Published SubmitOrder {OrderId} ✅", message.OrderId);` |
| 📨 | Receiving a message or response. Highlights inbound communication. | `logger.LogInformation("📨 Received response {Response} ✅", response);` |
| 🎉 | Notable event or celebration. | `// 🎉 Order processed successfully` |
| ✅ | Operation succeeded. Often appended to successful log statements. | `logger.LogInformation("✅ Payload: {Result}", result);` |
| ⚠️ | Warning or handled fault. Represents recoverable problems. | `logger.LogWarning(exception, "⚠️ Fault: {Message}", exception.Message);` |
| ❌ | Unhandled failure or fatal error. Use when the operation cannot continue. | `logger.LogError(exception, "❌ Failed to publish");` |

Use emojis sparingly so logs remain readable. Place them at the start of a message or near the action or result you want to emphasize.
