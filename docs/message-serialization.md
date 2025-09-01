# Message serialization

The primary content type used for the envelope is `application/vnd.mybus.envelope+json`. For compatibility the older
`application/vnd.masstransit+json` value is also recognized.

Raw messages in JSON use `application/json`.

If a message arrives without a `Content-Type` header, the envelope content type `application/vnd.mybus.envelope+json` is
assumed.
