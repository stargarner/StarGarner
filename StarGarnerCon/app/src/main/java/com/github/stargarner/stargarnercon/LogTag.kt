package com.github.stargarner.stargarnercon

import android.util.Log

class LogTag(private val tag:String){
    fun v(msg:String)= Log.v(tag,msg)
    fun d(msg:String)= Log.d(tag,msg)
    fun i(msg:String)= Log.i(tag,msg)
    fun w(msg:String)= Log.w(tag,msg)
    fun e(msg:String)= Log.e(tag,msg)
    fun v(ex:Throwable,msg:String)= Log.v(tag,msg,ex)
    fun d(ex:Throwable,msg:String)= Log.d(tag,msg,ex)
    fun i(ex:Throwable,msg:String)= Log.i(tag,msg,ex)
    fun w(ex:Throwable,msg:String)= Log.w(tag,msg,ex)
    fun e(ex:Throwable,msg:String="exception catched.")= Log.e(tag,msg,ex)
}
