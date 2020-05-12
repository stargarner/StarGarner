package com.github.stargarner.stargarnercon

import android.annotation.SuppressLint
import android.app.Dialog
import android.content.Context
import android.text.Editable
import android.text.TextWatcher
import android.text.method.LinkMovementMethod
import android.util.Log
import android.view.View
import android.widget.EditText
import android.widget.ImageButton
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import kotlinx.coroutines.suspendCancellableCoroutine
import okhttp3.*
import java.io.IOException
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException

private val httpClient = OkHttpClient.Builder().build()

class LogTag(private val tag: String) {
    fun e(ex: Throwable, msg: String = "error.") = Log.e(tag, msg, ex)
    fun d(msg: String) = Log.d(tag, msg)

//    fun v(msg: String) = Log.v(tag, msg)
//    fun i(msg: String) = Log.i(tag, msg)
//    fun w(msg: String) = Log.w(tag, msg)
//    fun e(msg: String) = Log.e(tag, msg)
//    fun v(ex: Throwable, msg: String) = Log.v(tag, msg, ex)
//    fun d(ex: Throwable, msg: String) = Log.d(tag, msg, ex)
//    fun i(ex: Throwable, msg: String) = Log.i(tag, msg, ex)
//    fun w(ex: Throwable, msg: String) = Log.w(tag, msg, ex)
}

fun Throwable.withCaption(fmt: String?, vararg args: Any) =
    "${
    if (fmt == null || args.isEmpty())
        fmt
    else
        String.format(fmt, *args)
    }: ${this.javaClass.simpleName} ${this.message}"

fun TextView.setTextIfChanged(str: CharSequence, goneIfEmpty: Boolean = true) {
    if (text.toString() != str.toString()) {
        text = str
    }
    if (goneIfEmpty) visibility = if (str.isEmpty()) View.GONE else View.VISIBLE
}

fun linkable(tv: TextView) = tv.apply {
    movementMethod = LinkMovementMethod.getInstance()
}

fun ImageButton.enableAlpha(enabled: Boolean) {
    isEnabled = enabled
    alpha = if (enabled) 1.0f else 0.3f
}

fun CharSequence.toast(context: Context) =
    Toast.makeText(context, this, Toast.LENGTH_LONG).show()

fun RequestBody.toPostRequestBuilder() = Request.Builder().post(this)

fun Request.call() = httpClient.newCall(this)

suspend fun Call.await(): Response {
    return suspendCancellableCoroutine { continuation ->
        enqueue(object : Callback {
            override fun onResponse(call: Call, response: Response) {
                continuation.resume(response)
            }

            override fun onFailure(call: Call, e: IOException) {
                // Don't bother with resuming the continuation if it is already cancelled.
                if (continuation.isCancelled) return
                continuation.resumeWithException(e)
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

@SuppressLint("InflateParams")
fun textDialog(
    activity: AppCompatActivity,
    initialText: String,
    validate: (String) -> String? = { null },
    onOk: (String) -> Unit
) {
    val view = activity.layoutInflater.inflate(R.layout.dlg_text_input, null, false)
    val editText: EditText = view.findViewById(R.id.editText)
    val btnCancel: View = view.findViewById(R.id.btnCancel)
    val btnOk: View = view.findViewById(R.id.btnOk)
    val tvError: TextView = view.findViewById(R.id.tvError)

    fun fireValidate() {
        val error = validate(editText.text.toString())
        btnOk.isEnabled = error == null
        tvError.setTextIfChanged(error ?: "")
    }

    val dialog = Dialog(activity)

    editText.setText(initialText)

    btnCancel.setOnClickListener {
        dialog.dismiss()
    }

    btnOk.setOnClickListener {
        onOk(editText.text.toString())
        dialog.dismiss()
    }

    editText.addTextChangedListener(object : TextWatcher {
        override fun beforeTextChanged(
            p0: CharSequence?,
            p1: Int,
            p2: Int,
            p3: Int
        ) {
        }

        override fun onTextChanged(p0: CharSequence?, p1: Int, p2: Int, p3: Int) {
        }

        override fun afterTextChanged(p0: Editable?) {
            fireValidate()
        }
    })

    fireValidate()
    dialog.setContentView(view)
    dialog.show()
}