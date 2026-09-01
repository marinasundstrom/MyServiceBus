package com.myservicebus.kotlin

/** An exhaustive Kotlin representation of one of two request response types. */
sealed interface RequestResult<out TFirst : Any, out TSecond : Any> {
    /** The request returned its first declared response type. */
    data class First<out T : Any>(val message: T) : RequestResult<T, Nothing>

    /** The request returned its second declared response type. */
    data class Second<out T : Any>(val message: T) : RequestResult<Nothing, T>
}
