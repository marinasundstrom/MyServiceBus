package com.myservicebus.kotlin

import com.myservicebus.MessageBus as JvmMessageBus
import kotlin.test.Test
import kotlin.test.assertFailsWith
import kotlin.time.Duration
import kotlin.time.Duration.Companion.seconds
import org.mockito.Mockito.mock
import org.mockito.Mockito.verify

class MessageBusTest {
    @Test
    fun `stop projects Kotlin durations and infinity`() {
        val delegate = mock(JvmMessageBus::class.java)
        val bus = MessageBus(delegate)

        bus.stop(5.seconds)
        verify(delegate).stop(java.time.Duration.ofSeconds(5))

        bus.stop(Duration.INFINITE)
        verify(delegate).stop()

        assertFailsWith<IllegalArgumentException> {
            bus.stop((-1).seconds)
        }
    }

    @Test
    fun `close stops the shared bus`() {
        val delegate = mock(JvmMessageBus::class.java)

        MessageBus(delegate).close()

        verify(delegate).stop()
    }
}
