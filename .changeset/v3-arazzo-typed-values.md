---
"bielu-aspnetcore-asyncapi": major
---

**Breaking:** `ArazzoStepAction`, `ArazzoParameterLocation`, `ArazzoSourceDescriptionType`, `ArazzoSuccessActionType` and `ArazzoFailureActionType` are now `readonly record struct`s instead of classes of `string` constants, and `ArazzoStep.Action`, `ArazzoParameter.In`, `ArazzoSourceDescription.Type`, `ArazzoSuccessAction.Type` and `ArazzoFailureAction.Type` use them. Strings convert implicitly, so assignments and `==` comparisons keep compiling. Code that reads one as a `string` now uses `.Value`, and `switch` cases on the old constants become `==` comparisons. The builders' `Channel`, `Parameter` and `AddSourceDescription` take the typed values. The BARAZZO004/BARAZZO005 analyzers recognize the static properties as well as string literals.
