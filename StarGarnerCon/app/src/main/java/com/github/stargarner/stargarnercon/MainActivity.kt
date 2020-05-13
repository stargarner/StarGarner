package com.github.stargarner.stargarnercon

import android.content.SharedPreferences
import android.os.Bundle
import android.os.SystemClock
import android.text.SpannableStringBuilder
import android.text.Spanned
import android.text.style.ClickableSpan
import android.view.View
import android.widget.ImageButton
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity
import kotlinx.coroutines.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import kotlin.coroutines.CoroutineContext
import kotlinx.coroutines.CancellationException

class MainActivity : AppCompatActivity(), CoroutineScope {
    companion object {
        private const val KEY_LAST_SERVER = "lastServer"

        private val log = LogTag("StarGarnerCon")

        private var reServerName = """\A([^:/#?]+|\[[:\dA-Fa-f]+]):\d+\z""".toRegex()
    }

    private lateinit var activityJob: Job

    override val coroutineContext: CoroutineContext
        get() = activityJob + Dispatchers.Main

    private lateinit var tvServer: TextView
    private lateinit var ibServerEdit: ImageButton

    private lateinit var tvStartTimeStar: TextView
    private lateinit var ibStartTimeEditStar: ImageButton

    private lateinit var tvStartTimeSeed: TextView
    private lateinit var ibStartTimeEditSeed: ImageButton

    private lateinit var tvHistoryStar: TextView
    private lateinit var tvHistorySeed: TextView
    private lateinit var tvStatus: TextView
    private lateinit var tvWaitReason: TextView
    private lateinit var tvOpenReason: TextView
    private lateinit var tvCloseReason: TextView

    private lateinit var pref: SharedPreferences

    @Volatile
    private var server: String = ""

    private var statusJob: Job? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        activityJob = Job()

        this.pref = getSharedPreferences("AppData", MODE_PRIVATE)

        setContentView(R.layout.activity_main)
        tvServer = findViewById(R.id.tvServer)
        ibServerEdit = findViewById(R.id.ibServerEdit)
        tvStartTimeStar = findViewById(R.id.tvStartTimeStar)
        ibStartTimeEditStar = findViewById(R.id.ibStartTimeEditStar)
        tvStartTimeSeed = findViewById(R.id.tvStartTimeSeed)
        ibStartTimeEditSeed = findViewById(R.id.ibStartTimeEditSeed)
        tvHistoryStar = linkable(findViewById(R.id.tvHistoryStar))
        tvHistorySeed = linkable(findViewById(R.id.tvHistorySeed))
        tvStatus = linkable(findViewById(R.id.tvStatus))
        tvWaitReason = linkable(findViewById(R.id.tvWaitReason))
        tvOpenReason = linkable(findViewById(R.id.tvOpenReason))
        tvCloseReason = linkable(findViewById(R.id.tvCloseReason))

        val sv = pref.getString(KEY_LAST_SERVER, null)
        if (sv != null) {
            tvServer.text = sv
            server = sv.trim()
        }

        ibServerEdit.setOnClickListener {
            textDialog(
                this,
                tvServer.text.toString(),
                validate = {
                    reServerName.find(it)
                        ?: return@textDialog "接続先が addr:port の形式ではありません"
                    null
                }
            ) { sv ->
                pref.edit().putString(KEY_LAST_SERVER, sv).apply()
                tvServer.text = sv
                server = sv.trim()
            }
        }

        ibStartTimeEditStar.setOnClickListener {
            editStartTime(
                "star",
                tvStartTimeStar.text.toString()
            )
        }

        ibStartTimeEditSeed.setOnClickListener {
            editStartTime(
                "seed",
                tvStartTimeSeed.text.toString()
            )
        }

        showStatus(JSONObject().apply { put("error", "initializing…") })
    }

    override fun onDestroy() {
        log.d("onDestroy")
        super.onDestroy()
        (activityJob + Dispatchers.Default).cancel()
    }

    override fun onStart() {
        log.d("onStart")
        super.onStart()
        statusJob = launch(Dispatchers.Default) { loopStatus() }
    }

    override fun onStop() {
        log.d("onStop")
        statusJob?.cancel()
        super.onStop()
    }

    private fun showStatus(status: JSONObject) {
        val error = status.optString("error")
        when {
            error.isNotEmpty() -> {
                tvStatus.setTextIfChanged(error)
                tvStartTimeStar.setTextIfChanged("", goneIfEmpty = false)
                tvStartTimeSeed.setTextIfChanged("", goneIfEmpty = false)
                tvHistoryStar.setTextIfChanged("")
                tvHistorySeed.setTextIfChanged("")
                tvWaitReason.setTextIfChanged("")
                tvOpenReason.setTextIfChanged("")
                tvCloseReason.setTextIfChanged("")
                ibStartTimeEditStar.enableAlpha(false)
                ibStartTimeEditSeed.enableAlpha(false)
            }

            status.optInt("isLogin") != 1 -> {
                tvStatus.setTextIfChanged("接続先があのサイトにログインしていません")
                tvStartTimeStar.setTextIfChanged("", goneIfEmpty = false)
                tvStartTimeSeed.setTextIfChanged("", goneIfEmpty = false)
                tvHistoryStar.setTextIfChanged("")
                tvHistorySeed.setTextIfChanged("")
                tvWaitReason.setTextIfChanged("")
                tvOpenReason.setTextIfChanged("")
                tvCloseReason.setTextIfChanged("")
                ibStartTimeEditStar.enableAlpha(false)
                ibStartTimeEditSeed.enableAlpha(false)
            }
            else -> {
                tvStatus.setTextIfChanged(status.optString("status"))
                tvStartTimeStar.setTextIfChanged(
                    status.optString("startTimeStar"),
                    goneIfEmpty = false
                )
                tvStartTimeSeed.setTextIfChanged(
                    status.optString("startTimeSeed"),
                    goneIfEmpty = false
                )
                tvHistoryStar.setTextIfChanged(status.optString("historyStar"))
                tvHistorySeed.setTextIfChanged(status.optString("historySeed"))
                tvWaitReason.setTextIfChanged(linkForceOpen(status.optString("waitReason")))
                tvOpenReason.setTextIfChanged(status.optString("openReason"))
                tvCloseReason.setTextIfChanged(status.optString("closeReason"))
                ibStartTimeEditStar.enableAlpha(true)
                ibStartTimeEditSeed.enableAlpha(true)
            }
        }
    }

    private suspend fun loopStatus() {
        log.d("loopStatus: start.")
        val currentJob = coroutineContext[Job]!!
        while (true) {
            val timeStart = SystemClock.elapsedRealtime()
            val status = try {
                if (server.isEmpty()) error("server is not specified.")
                val url = "http://${server}/status?_=$timeStart"
                val response = Request.Builder().url(url).build().call().await()
                if (!response.isSuccessful) error("response error: $response")
                @Suppress("BlockingMethodInNonBlockingContext")
                JSONObject(response.body?.string() ?: error("missing response body"))
            } catch (ex: CancellationException) {
                log.d("loopStatus: request was cancelled.")
                break
            } catch (ex: Throwable) {
                log.e(ex, "request failed.")
                JSONObject().apply {
                    put("error", ex.withCaption("can't get status"))
                }
            }
            withContext(Dispatchers.Main) {
                if (!isDestroyed) showStatus(status)
            }
            try {
                val remain = timeStart + 1000L - SystemClock.elapsedRealtime()
                if (remain > 0 && !currentJob.isCancelled)
                    delay(remain)
            } catch (ex: CancellationException) {
                log.d("loopStatus: delay was cancelled.")
                break
            } catch (ex: Throwable) {
                log.e(ex, "delay failed.")
            }
        }
        log.d("loopStatus: end.")
    }

    private fun CharSequence.toast() = toast(this@MainActivity)

    // 入力テキストの「強制的に開く」をリンク化する
    private fun linkForceOpen(src: String) = SpannableStringBuilder().also { dst ->
        for (line in src.split("\n")) {
            if (dst.isNotEmpty()) dst.append("\n")
            dst.append(line)
            val kind = when {
                line.startsWith("星") -> "star"
                line.startsWith("種") -> "seed"
                else -> null
            }
            val linkWord = "強制的に開く"
            if (kind == null || !line.endsWith(linkWord)) continue
            dst.setSpan(
                object : ClickableSpan() {
                    override fun onClick(v: View) {
                        launch(Dispatchers.Default) {
                            try {
                                val url = "http://${server}/forceOpen"
                                val response =
                                    JSONObject().apply { put("kind", kind) }
                                        .toString()
                                        .toRequestBody("application/json".toMediaType())
                                        .toPostRequestBuilder()
                                        .url(url)
                                        .build().call().await()
                                if (!response.isSuccessful) error("response error: $response")
                            } catch (ex: Throwable) {
                                log.e(ex, "can't post forceOpen")
                                ex.withCaption("can't post forceOpen.").toast()
                            }
                        }
                    }
                },
                dst.length - linkWord.length,
                dst.length,
                Spanned.SPAN_EXCLUSIVE_EXCLUSIVE
            )
        }
    }

    // 配信開始時刻を編集して送信する
    private fun editStartTime(kind: String, initialText: String) {
        textDialog(this, initialText) { newText ->
            launch(Dispatchers.IO) {
                try {
                    val url = "http://${server}/startTime"
                    val response =
                        JSONObject().apply {
                            put("kind", kind)
                            put("value", newText)
                        }
                            .toString()
                            .toRequestBody("application/json".toMediaType())
                            .toPostRequestBuilder()
                            .url(url)
                            .build().call().await()
                    if (!response.isSuccessful) error("response error: $response")
                } catch (ex: Throwable) {
                    log.e(ex, "can't post startTime.")
                    ex.withCaption("can't post startTime.").toast()
                }
            }
        }
    }
}
