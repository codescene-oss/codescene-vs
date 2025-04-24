const GlobalSettings = {
    FeatureAEnabled: true,
    FeatureBEnabled: true,
    DebugMode: true
};

const GlobalUtilities = {
    GlobalCounter: 0,
    SomeGlobalString: "Hello from the global scope!",

    CheckCondition: function (value) {
        return GlobalSettings.FeatureBEnabled && (value % 2 === 0);
    }
};

class DeepGlobalNestedComplexityExample {


    
    ComplexMethod(value) {
        if (GlobalSettings.FeatureAEnabled) {

            if (value > 10) {

                for (let i = 0; i < value; i++) {

                    if (GlobalSettings.DebugMode) {
                        console.log(`[Debug] Iteration ${i}, Value = ${value}`);

                        if (GlobalUtilities.SomeGlobalString.includes("Hello")) {

                            while (GlobalUtilities.GlobalCounter < i) {

                                if (GlobalUtilities.CheckCondition(i)) {
                                    console.log("Condition met � incrementing GlobalCounter.");
                                    GlobalUtilities.GlobalCounter++;
                                } else {
                                    console.log("Condition not met � resetting GlobalCounter.");
                                    GlobalUtilities.GlobalCounter = 0;
                                }
                            }
                        }
                    } else {

                        if (GlobalUtilities.CheckCondition(i)) {
                            console.log("Operation in non-debug mode, condition was true.");
                        }
                    }
                }
            } else {
                console.log("Value must be greater than 10 to proceed with complex logic.");
            }
        } else {
            console.log("Feature A is not enabled.");
        }
    }
}

const example = new DeepGlobalNestedComplexityExample();
example.ComplexMethod(15);
