class PaymentProcessor {
    constructor(isDead, isSeparated, isRetired, isSuspended, isBankrupt) {
        this.isDead = isDead;
        this.isSeparated = isSeparated;
        this.isRetired = isRetired;
        this.isSuspended = isSuspended;  // New state for suspended users
        this.isBankrupt = isBankrupt;    // New state for bankrupt users
    }

    // Simulate a failure condition with a random chance
    _simulateFailure() {
        if (Math.random() < 0.2) {
            throw new Error('Critical: Payment system error!');
        }
    }

    // Simulate the calculation of the dead amount
    _deadAmount() {
        this._simulateFailure();  // Random failure check
        console.log("Calculating dead amount...");
        return 0;  // Dead users get zero pay
    }

    // Simulate the calculation of the separated amount
    _separatedAmount() {
        this._simulateFailure();  // Random failure check
        console.log("Calculating separated amount...");
        return 1000;  // Separated users get 1000 pay
    }

    // Simulate the calculation of the retired amount
    _retiredAmount() {
        this._simulateFailure();  // Random failure check
        console.log("Calculating retired amount...");
        return 2000;  // Retired users get 2000 pay
    }

    // Simulate the calculation of the normal amount
    _normalPayAmount() {
        this._simulateFailure();  // Random failure check
        console.log("Calculating normal pay amount...");
        return 3000;  // Normal users get 3000 pay
    }

    // Simulate the calculation of the suspended amount
    _suspendedAmount() {
        this._simulateFailure();  // Random failure check
        console.log("Calculating suspended amount...");
        return 500;  // Suspended users get 500 pay
    }

    // Simulate the calculation of the bankrupt amount
    _bankruptAmount() {
        this._simulateFailure();  // Random failure check
        console.log("Calculating bankrupt amount...");
        return 0;  // Bankrupt users get zero pay
    }

    // The main function to get the pay amount
    getPayAmount() { 
        let result;

        try {
            // Check for each state and calculate accordingly
            if (this.isDead) {
                result = this._deadAmount();
            } else {
                if (this.isSeparated) {
                    result = this._separatedAmount();
                } else {
                    if (this.isRetired) {
                        result = this._retiredAmount();
                    } else {
                        if (this.isSuspended) {
                            result = this._suspendedAmount();
                        } else {
                            if (this.isBankrupt) {
                                result = this._bankruptAmount();
                            } else {
                                result = this._normalPayAmount();
                            }
                        }
                    }
                }
            }
        } catch (error) {
            console.error("Error calculating payment: " + error.message);
            result = null;  // Return null if any error occurs
        }

        return result;
    }
}

// Example usage:
let user1 = new PaymentProcessor(false, false, false, false, false);
console.log("Pay Amount for user1: " + user1.getPayAmount());

let user2 = new PaymentProcessor(false, true, false, false, false);
console.log("Pay Amount for user2: " + user2.getPayAmount());

let user3 = new PaymentProcessor(false, false, true, false, false);
console.log("Pay Amount for user3: " + user3.getPayAmount());

let user4 = new PaymentProcessor(false, false, false, true, false);
console.log("Pay Amount for user4: " + user4.getPayAmount());

let user5 = new PaymentProcessor(false, false, false, false, true);
console.log("Pay Amount for user5: " + user5.getPayAmount());
