package com.github.stargarner.stargarnercon

import android.content.SharedPreferences
import android.os.Bundle
import android.text.SpannableStringBuilder
import android.text.Spanned
import android.text.style.ClickableSpan
import android.view.View
import android.widget.ImageButton
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import kotlinx.coroutines.*
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import kotlin.coroutines.CoroutineContext

class MainActivity : AppCompatActivity(), CoroutineScope {
    companion object {
        private const val KEY_LAST_SERVER = "lastServer"

        private val log = LogTag("${App1.TAG}:MainActivity")

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

    @Volatile
    private var server: String = ""
    private lateinit var pref: SharedPreferences

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
            server = tvServer.text.toString().trim()
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
                this.server = sv.trim()
            }
        }

        ibStartTimeEditStar.setOnClickListener { editStartTime("star", tvStartTimeStar) }
        ibStartTimeEditSeed.setOnClickListener { editStartTime("seed", tvStartTimeSeed) }
        tvWaitReason.text = "" // 復元時にリンクが失われるので再生成

        showStatus(JSONObject().apply { put("error", "initializing…") })
    }

    override fun onDestroy() {
        super.onDestroy()
        (activityJob + Dispatchers.Default).cancel()
    }

    override fun onStart() {
        super.onStart()
        statusJob = launch(Dispatchers.Default) { loadStatus() }
    }

    override fun onStop() {
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
            !status.optBoolean("isLogin") -> {
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

    private suspend fun loadStatus() {
        while (coroutineContext[Job]?.isCancelled == false) {
            try {
                val status = try {
                    if (server.isEmpty()) error("server is not specified.")
                    val url = "http://${server}/status"
                    val response = Request.Builder().url(url).build().call().await()
                    if (!response.isSuccessful) error("response error: $response")
                    @Suppress("BlockingMethodInNonBlockingContext")
                    JSONObject(response.body?.string() ?: error("missing response body"))
                } catch (ex: Throwable) {
                    log.e(ex)
                    JSONObject().apply {
                        put("error", ex.withCaption("can't get status"))
                    }
                }
                withContext(Dispatchers.Main) {
                    showStatus(status)
                }
            } catch (ex: Throwable) {
                log.e(ex)
            } finally {
                delay(1000L)
            }
        }
    }

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
            if (kind != null && line.endsWith(linkWord)) {
                dst.setSpan(
                    object : ClickableSpan() {
                        override fun onClick(v: View) {
                            launch(Dispatchers.Default) {
                                try {
                                    val url = "http://${server}/forceOpen"
                                    val response = Request.Builder().url(url)
                                        .post(
                                            JSONObject().apply { put("kind", kind) }
                                                .toString()
                                                .toRequestBody("application/json".toMediaType())
                                        )
                                        .build().call().await()
                                    if (!response.isSuccessful) error("response error: $response")
                                } catch (ex: Throwable) {
                                    log.e(ex, "can't send forceOpen")
                                    Toast.makeText(
                                        this@MainActivity,
                                        ex.withCaption("can't send forceOpen"),
                                        Toast.LENGTH_LONG
                                    ).show()
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
    }


    private fun editStartTime(kind: String, tv: TextView) {
        textDialog(this, tv.text.toString()) { sv ->
            launch(Dispatchers.IO) {
                try {
                    val url = "http://${server}/startTime"
                    val response = Request.Builder().url(url)
                        .post(
                            JSONObject().apply {
                                put("kind", kind)
                                put("value", sv)
                            }
                                .toString().toRequestBody("application/json".toMediaType())
                        )
                        .build().call().await()
                    if (!response.isSuccessful) error("response error: $response")
                    withContext(Dispatchers.Main) {
                        tv.text = sv
                    }
                } catch (ex: Throwable) {
                    log.e(ex)
                    Toast.makeText(
                        this@MainActivity,
                        ex.withCaption("can't send startTime"),
                        Toast.LENGTH_LONG
                    ).show()
                }
            }
        }
    }
}
