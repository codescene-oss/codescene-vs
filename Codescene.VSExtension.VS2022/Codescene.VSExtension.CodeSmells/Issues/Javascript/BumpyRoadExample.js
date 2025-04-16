const fs = require('fs');
const path = require('path');

class BumpyRoadExample {
    processDirectory(dirPath) {

        const files = [];

        const items = fs.readdirSync(dirPath);

        items.forEach(item => {
            const fullPath = path.join(dirPath, item);

            if (fs.statSync(fullPath).isFile()) {
                if (/^data\d+\.csv$/.test(item)) {
                    files.push(fullPath);
                }
            }
        });

        let sb = "";

        files.forEach(filePath => {
            const data = fs.readFileSync(filePath, 'utf8');

            const lines = data.split(/\r?\n/);
            lines.forEach(line => {
                sb += line;
            });
        });

        fs.writeFileSync('data.csv', sb, 'utf8');
    }
}

const example = new BumpyRoadExample();
example.processDirectory('./some_directory');