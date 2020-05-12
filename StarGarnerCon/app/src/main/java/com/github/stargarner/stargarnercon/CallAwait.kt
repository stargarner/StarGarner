@file:Suppress("RedundantVisibilityModifier")

package com.github.stargarner.stargarnercon

import kotlinx.coroutines.suspendCancellableCoroutine
import okhttp3.Call
import okhttp3.Callback
import okhttp3.Response
import java.io.IOException
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException

public const val OKHTTP_STACK_RECORDER_PROPERTY = "ru.gildor.coroutines.okhttp.stackrecorder"

/**
 * Debug turned on value for [DEBUG_PROPERTY_NAME]. See [newCoroutineContext][CoroutineScope.newCoroutineContext].
 */
public const val OKHTTP_STACK_RECORDER_ON = "on"

/**
 * Debug turned on value for [DEBUG_PROPERTY_NAME]. See [newCoroutineContext][CoroutineScope.newCoroutineContext].
 */
public const val OKHTTP_STACK_RECORDER_OFF = "off"

@JvmField
val isRecordStack = when (System.getProperty(OKHTTP_STACK_RECORDER_PROPERTY)) {
    OKHTTP_STACK_RECORDER_ON -> true
    OKHTTP_STACK_RECORDER_OFF, null, "" -> false
    else -> error("System property '$OKHTTP_STACK_RECORDER_PROPERTY' has unrecognized value '${System.getProperty(OKHTTP_STACK_RECORDER_PROPERTY)}'")
}


/**
 * Suspend extension that allows suspend [Call] inside coroutine.
 *
 * [recordStack] enables track recording, so in case of exception stacktrace will contain call stacktrace, may be useful for debugging
 *      Not free! Creates exception on each request so disabled by default, but may be enabled using system properties:
 *
 *      ```
 *      System.setProperty(OKHTTP_STACK_RECORDER_PROPERTY, OKHTTP_STACK_RECORDER_ON)
 *      ```
 *      see [README.md](https://github.com/gildor/kotlin-coroutines-okhttp/blob/master/README.md#Debugging) with details about debugging using this feature
 *
 * @return Result of request or throw exception
 */
public suspend fun Call.await(recordStack: Boolean = isRecordStack): Response {
    val callStack = if (recordStack) {
        IOException().apply {
            // Remove unnecessary lines from stacktrace
            // This doesn't remove await$default, but better than nothing
            stackTrace = stackTrace.copyOfRange(1, stackTrace.size)
        }
    } else {
        null
    }
    return suspendCancellableCoroutine { continuation ->
        enqueue(object : Callback {
            override fun onResponse(call: Call, response: Response) {
                continuation.resume(response)
            }

            override fun onFailure(call: Call, e: IOException) {
                // Don't bother with resuming the continuation if it is already cancelled.
                if (continuation.isCancelled) return
                callStack?.initCause(e)
                continuation.resumeWithException(callStack ?: e)
            }
        })

        continuation.invokeOnCancellation {
            try {
                cancel()
            } catch (ex: Throwable) {
                //Ignore cancel exception
            }
        }
    }
}
