package com.github.stargarner.stargarnercon

import android.annotation.SuppressLint
import android.app.Dialog
import android.text.Editable
import android.text.TextWatcher
import android.view.View
import android.widget.EditText
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity

class DlgTextInput{

    companion object{

        @SuppressLint("InflateParams")
        fun show(
            activity:AppCompatActivity,
            initialText:String,
            validate:(String)->String? = {null},
            onOk:(String)->Unit
        ){
            val view = activity.layoutInflater.inflate(R.layout.dlg_text_input,null,false)
            val editText: EditText = view.findViewById(R.id.editText)
            val btnCancel : View = view.findViewById(R.id.btnCancel)
            val btnOk :View = view.findViewById(R.id.btnOk)
            val tvError :TextView = view.findViewById(R.id.tvError)

            fun fireValidate(){
                val error = validate(editText.text.toString())
                btnOk.isEnabled = error == null
                tvError.setTextIfChanged(error?:"")
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

            editText.addTextChangedListener(object:TextWatcher{
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
    }
}