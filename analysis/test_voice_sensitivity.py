import json
import math
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

import numpy as np
from scipy import stats

from voice_sensitivity import detectable_dz, mean_ar1_variance, paired_power, recruitment_for_completion, run, simulate, theta_categories, validate, wilson

CONFIG = Path(__file__).parent/'power/voice_sensitivity.json'


class VoiceSensitivityTests(unittest.TestCase):
    def config(self):
        return json.loads(CONFIG.read_text())

    def test_current_signed_step_and_legacy_geometry_allocations(self):
        config = self.config()
        validate(config)
        for size, quota in ((32, [4,8,8,8,4]), (40, [8,8,8,8,8])):
            draws = theta_categories(np.random.default_rng(17), (3,2), size)
            for block in draws.reshape(-1,size):
                self.assertEqual(np.bincount(block).tolist(), quota)
        config.update(version='voice-sensitivity-proposal-v1', trials_per_voice=[10,40])
        validate(config)
        config['trials_per_voice'] = [32]
        with self.assertRaises(ValueError): validate(config)

    def test_paired_benchmark_null_symmetry_and_inverse(self):
        self.assertAlmostEqual(paired_power(48,0,.025),.025,places=10)
        self.assertAlmostEqual(paired_power(20,-.5,.05),paired_power(20,.5,.05),places=10)
        self.assertAlmostEqual(paired_power(48,3.9743589743589745,.025),1,places=10)
        d=detectable_dz(48,.025,.8)
        self.assertTrue(.45 < d < .47)
        self.assertAlmostEqual(paired_power(48,d,.025),.8,places=9)
        self.assertGreater(detectable_dz(15,.05,.8),detectable_dz(20,.05,.8))

    def test_trial_mean_variance_matches_covariance_sum_and_has_slope_floor(self):
        for m,rho in [(1,.5),(10,0),(40,.6),(10,-.4)]:
            covariance=.6**2*rho**np.abs(np.subtract.outer(np.arange(m),np.arange(m)))
            self.assertAlmostEqual(mean_ar1_variance(m,.6,rho),float(covariance.mean()),places=12)
        self.assertGreater(mean_ar1_variance(40,.6,.5),mean_ar1_variance(40,.6,0))
        self.assertGreater(.2**2+2*mean_ar1_variance(10000,.6,.5),.2**2)

    def test_complete_simulation_agrees_with_order_adjusted_gaussian_power(self):
        config=self.config();config['replications']=5000
        scenario=config['scenarios'][1];n,m,ratio=20,40,.9
        variance=scenario['voice_slope_sd']**2+config['period_person_sd']**2+2*mean_ar1_variance(m,scenario['trial_sd'],scenario['ar1'])
        cutoff=stats.t.isf(config['alpha']/2,n-2)
        ncp=abs(math.log(ratio))*math.sqrt(n/variance)
        expected=stats.nct.sf(cutoff,n-2,ncp)+stats.nct.cdf(-cutoff,n-2,ncp)
        result=simulate(config,scenario,n,m,ratio,17)
        self.assertEqual(result['estimable_replications'],5000)
        self.assertEqual(result['mean_retained_participants'],n)
        self.assertLess(abs(result['rejection_rate_all']-expected),5*math.sqrt(expected*(1-expected)/5000))
        self.assertLess(abs(result['bias_log_ratio']),5*math.sqrt(variance/(n*5000)))

    def test_null_calibration_and_intercept_period_cancellation(self):
        config=self.config();config['replications']=3000
        scenario=config['scenarios'][0]
        result=simulate(config,scenario,20,10,1,29)
        self.assertLess(abs(result['rejection_rate_all']-.05),5*math.sqrt(.05*.95/3000))
        config['participant_intercept_sd']=4;config['period_log_effect']=1.5
        other=simulate(config,scenario,20,10,1,29)
        self.assertEqual(result['rejections'],other['rejections'])
        self.assertAlmostEqual(result['mean_estimated_log_ratio'],other['mean_estimated_log_ratio'],places=12)

    def test_seed_loss_and_informative_missingness_are_explicit(self):
        config=self.config();config['replications']=300
        scenario=config['scenarios'][-1]
        first=simulate(config,scenario,20,40,1,11)
        self.assertEqual(first,simulate(config,scenario,20,40,1,11))
        self.assertLess(first['mean_retained_participants'],20)
        self.assertLess(first['bias_log_ratio'],-.025)
        self.assertEqual(first['interpretation'],'null rejection rate')
        heavy=dict(scenario,participant_loss=.999999,informative_loss_strength=0)
        result=simulate(config,heavy,6,5,1,11)
        self.assertEqual(result['estimable_replications'],0)
        self.assertIsNone(result['rejection_rate_estimable'])
        self.assertIsNone(result['mean_estimated_log_ratio'])

    def test_invalid_assumptions_are_rejected(self):
        for key,value in [('trials_per_voice',[41]),('participants',[True]),('replications',0),('alpha',float('nan')),('time_ratios',[0]),('minimum_block_fraction',1.1)]:
            config=self.config();config[key]=value
            with self.assertRaises(ValueError):validate(config)
        config=self.config();config['scenarios'][0]['ar1']=1
        with self.assertRaises(ValueError):validate(config)
        self.assertEqual(wilson(0,0),[None,None])
        self.assertLess(wilson(50,100)[0],.5);self.assertGreater(wilson(50,100)[1],.5)

    def test_recruitment_separates_expected_count_from_completion_assurance(self):
        self.assertEqual(recruitment_for_completion(20,1,.9),20)
        count=recruitment_for_completion(20,.85,.9)
        self.assertGreater(count,math.ceil(20/.85))
        self.assertGreaterEqual(stats.binom.sf(19,count,.85),.9)
        self.assertLess(stats.binom.sf(19,count-1,.85),.9)

    def test_artifacts_config_order_independence_and_cli(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);config=self.config()
            config.update(replications=20,participants=[15],trials_per_voice=[32],time_ratios=[1,.9],scenarios=[config['scenarios'][0]])
            path=root/'config.json';path.write_text(json.dumps(config))
            first=run(path,root/'one',plots=True)
            self.assertEqual(json.loads((root/'one/sensitivity.json').read_text()),first)
            self.assertEqual((root/'one/configuration.json').read_bytes(),path.read_bytes())
            config['time_ratios'].reverse();path.write_text(json.dumps(config))
            second=run(path,root/'two',plots=False)
            self.assertEqual(first['simulation'],list(reversed(second['simulation'])))
            with self.assertRaises(ValueError):run(path,root/'one',plots=False)
            command=[sys.executable,str(Path(__file__).with_name('voice_sensitivity.py')),'--config',str(path),'--out',str(root/'cli'),'--no-plots']
            result=subprocess.run(command,capture_output=True,text=True)
            self.assertEqual(result.returncode,0,result.stderr)
            self.assertIn('final study power remains unestablished',result.stdout)
            self.assertNotEqual(subprocess.run(command,capture_output=True).returncode,0)
