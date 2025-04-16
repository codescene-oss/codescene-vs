class DeepNestedComplexity {
    constructor() {
        // Initialize properties. You can change these as needed.
        this.isDead = false;
        this.isSeparated = false;
        this.isRetired = false;
    }

    // Private methods (by convention, using underscore)
    _deadAmount() {
        return 0;
    }

    _separatedAmount() {
        return 100;
    }

    _retiredAmount() {
        return 200;
    }

    _normalPayAmount() {
        return 300;
    }

    // Public method: gets the pay amount based on the nested conditionals.
    getPayAmount() {
        let result;
        if (this.isDead) {
            result = this._deadAmount();
        } else {
            if (this.isSeparated) {
                result = this._separatedAmount();
            } else {
                if (this.isRetired) {
                    result = this._retiredAmount();
                } else {
                    result = this._normalPayAmount();
                }
            }
        }
        return result;
    }
}

// Example usage:
const example = new DeepNestedComplexity();

// Set conditions
example.isDead = false;
example.isSeparated = true;
example.isRetired = false;

console.log(example.getPayAmount()); // Output: 100, since isSeparated is true
