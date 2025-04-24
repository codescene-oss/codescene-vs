class ComplexConditionalExample {
    ComplexConditional(value1, value2) {
        if (value1 > 10 && (value2 < 5 || value2 === 100)) {
            return "Condition A met";
        } else if (value1 <= 10 && value2 !== 100) {
            return "Condition B met";
        } else {
            return "None of the above";
        }
    }
}

// Example usage:
const example = new ComplexConditionalExample();
console.log(example.ComplexConditional(15, 3));   // "Condition A met"
console.log(example.ComplexConditional(8, 50));   // "Condition B met"
console.log(example.ComplexConditional(12, 50));  // "None of the above"
