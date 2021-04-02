import inquirer from 'inquirer';
import SimulationController from './controller';
import { SimulationConfig } from './types';

async function main() {
    
    const config = await inquirer.prompt<SimulationConfig>([
        {
            name: 'deviceId',
            message: 'Specify the deviceId:',
            type: 'input'
        },
        {
            name: 'idScope',
            message: 'Specify the device scopeId:',
            type: 'input'
        },
        {
            name: 'deviceKey',
            message: 'Specify the device key:',
            type: 'input'
        },
        {
            name: 'gatewayName',
            message: 'Specify the gateway host name:',
            type: 'input'
        }
    ]);

    const controller = new SimulationController(config);
    controller.start();
}

main().catch((err) => console.log(err));