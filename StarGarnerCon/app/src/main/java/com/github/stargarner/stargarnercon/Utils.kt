package com.github.stargarner.stargarnercon

import android.text.method.LinkMovementMethod
import android.view.View
import android.widget.ImageButton
import android.widget.TextView

fun Throwable.withCaption(fmt: String?, vararg args: Any) =
    "${
    if (fmt == null || args.isEmpty())
        fmt
    else
        String.format(fmt, *args)
    }: ${this.javaClass.simpleName} ${this.message}"

fun TextView.setTextIfChanged(str: CharSequence, goneIfEmpty: Boolean = true) {
    if (text.toString() != str.toString()){
        text = str
    }
    if (goneIfEmpty) visibility = if (str.isEmpty()) View.GONE else View.VISIBLE
}

fun linkable(tv: TextView) = tv.apply {
    movementMethod = LinkMovementMethod.getInstance()
}

fun ImageButton.enableAlpha(enabled:Boolean){
    isEnabled = enabled
    alpha = if(enabled) 1.0f else 0.3f
}