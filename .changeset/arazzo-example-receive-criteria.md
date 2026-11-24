---
---

The ArazzoScalar example's receive steps compare `$message.payload#/status` instead of filtering the single payload object with JSONPath, which never matched.
